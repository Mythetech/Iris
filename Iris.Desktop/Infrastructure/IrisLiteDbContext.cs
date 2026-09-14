using LiteDB;
using Iris.Components.Shared.DynamicTabs;
using Iris.Desktop.Brokers;
using Iris.Desktop.History;
using Iris.Desktop.PackageManagement;
using Iris.Desktop.Templates;
using Microsoft.Extensions.Logging;

namespace Iris.Desktop.Infrastructure
{
    public class IrisLiteDbContext : IDisposable
    {
        private static string DatabaseName { get; } = "IrisDb.db";

        /// <summary>
        /// Stamped on the database so a future breaking change to any collection has a
        /// version to migrate from. Nothing reads it yet beyond recording it; the point
        /// is that a database written today can be told apart from one written after the
        /// first schema change, which is impossible once records are already on disk.
        /// </summary>
        public const int SchemaVersion = 1;

        private readonly LiteDatabase _database;
        private readonly ILogger<IrisLiteDbContext> _logger;
        private bool _disposed;

        /// <summary>
        /// Builds every persisted type's entity mapper once, up front.
        ///
        /// <para>
        /// LiteDB maps a type on first use, into the process-wide <see cref="BsonMapper.Global"/>.
        /// Two threads reaching an unmapped type at the same time can have one of them observe
        /// a mapper the other is still filling in, which surfaces as
        /// <c>NotSupportedException: Member X not found on BsonMapper for type Y</c> from an
        /// EnsureIndex or a query, for a member that plainly exists. Iris writes history from
        /// the send path while pages read from the render thread, so that is reachable on a
        /// cold start; it showed up first as two test classes touching HistoryRepository at
        /// once.
        /// </para>
        ///
        /// <para>
        /// A static constructor is the cheap fix: the runtime guarantees it runs once, and
        /// nothing can reach a collection without going through this class. HistoryRecord is
        /// here as well as PersistentHistoryRecord because the indexed member is declared on
        /// the base, and that is the mapper LiteDB resolves it against.
        /// </para>
        /// </summary>
        static IrisLiteDbContext()
        {
            BsonMapper.Global.Entity<SavedConnection>();
            BsonMapper.Global.Entity<PersistentTemplate>();
            BsonMapper.Global.Entity<SavedPackage>();
            BsonMapper.Global.Entity<Iris.History.HistoryRecord>();
            BsonMapper.Global.Entity<PersistentHistoryRecord>();
            BsonMapper.Global.Entity<PersistentMessagingLayout>();
            BsonMapper.Global.Entity<SerializedDynamicTabModel>();
        }

        public IrisLiteDbContext(ILogger<IrisLiteDbContext> logger)
            : this(logger, GetDatabasePath())
        {
        }

        /// <summary>
        /// Takes the file explicitly so a test can run against a temporary database rather
        /// than the developer's real one. DI selects the single-argument constructor,
        /// because a string is not something the container can resolve.
        /// </summary>
        public IrisLiteDbContext(ILogger<IrisLiteDbContext> logger, string dbPath)
        {
            _logger = logger;
            var connectionString = new ConnectionString
            {
                Filename = dbPath,

                // Shared, not Direct, even though this is now a single long-lived
                // connection. Direct takes an exclusive lock on the file, and nothing
                // stops a user from launching Iris twice; the second one would fail to
                // open its own database rather than simply being slower.
                Connection = ConnectionType.Shared
            };

            try
            {
                _database = new LiteDatabase(connectionString);
                _database.GetCollectionNames();
            }
            catch (LiteException ex) when (ex.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase)
                                           || ex.ErrorCode == LiteException.INVALID_DATABASE)
            {
                _database?.Dispose();

                var quarantinePath = Quarantine(dbPath);

                _logger.LogError(ex,
                    "LiteDB database at {Path} could not be opened and has been moved to {QuarantinePath}. " +
                    "A new empty database was created. The previous connections, history, templates and packages " +
                    "remain in the quarantined file and are not lost.",
                    dbPath, quarantinePath);

                _database = new LiteDatabase(connectionString);
            }

            if (_database.UserVersion == 0)
                _database.UserVersion = SchemaVersion;
        }

        /// <summary>
        /// Moves an unreadable database aside rather than deleting it. The file holds the
        /// user's connections, history, templates and package manifests, so a transient
        /// read failure must never be able to destroy it. The log file moves with it,
        /// otherwise a stale write-ahead log would be picked up by the replacement database.
        /// </summary>
        private static string Quarantine(string dbPath)
        {
            var directory = Path.GetDirectoryName(dbPath)!;
            var stem = Path.GetFileNameWithoutExtension(dbPath);
            var extension = Path.GetExtension(dbPath);
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

            var quarantinePath = Path.Combine(directory, $"{stem}.corrupt-{stamp}{extension}");

            File.Move(dbPath, quarantinePath);

            var logPath = Path.Combine(directory, $"{stem}-log{extension}");
            if (File.Exists(logPath))
            {
                File.Move(logPath, Path.Combine(directory, $"{stem}-log.corrupt-{stamp}{extension}"));
            }

            return quarantinePath;
        }

        /// <summary>The default location, alongside the user's other application data.</summary>
        private static string GetDatabasePath()
        {
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folderPath = Path.Combine(basePath, "Iris");
            Directory.CreateDirectory(folderPath);
            return Path.Combine(folderPath, DatabaseName);
        }

        /// <summary>The schema version recorded in the file this context opened.</summary>
        public int UserVersion => _database.UserVersion;

        /// <summary>
        /// The index names defined on a collection, read from LiteDB's own catalogue.
        /// A missing index is invisible until a collection is large enough to hurt, so it
        /// is worth being able to assert on.
        /// </summary>
        public IReadOnlyList<string> ListIndexes(string collection)
        {
            return _database.GetCollection("$indexes")
                .Find(LiteDB.Query.EQ("collection", collection))
                .Select(document => document["name"].AsString)
                .ToList();
        }

        public ILiteCollection<T> GetCollection<T>(string? name = null)
            where T : ILocalEntity
        {
            return _database.GetCollection<T>(name ?? typeof(T).Name);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _database?.Dispose();
                }

                _disposed = true;
            }
        }
    }
}
