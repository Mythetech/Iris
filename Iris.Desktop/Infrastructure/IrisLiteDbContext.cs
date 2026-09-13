using LiteDB;
using Microsoft.Extensions.Logging;

namespace Iris.Desktop.Infrastructure
{
    public class IrisLiteDbContext : IDisposable
    {
        private static string DatabaseName { get; } = "IrisDb.db";
        private readonly LiteDatabase _database;
        private readonly ILogger<IrisLiteDbContext> _logger;
        private bool _disposed;

        public IrisLiteDbContext(ILogger<IrisLiteDbContext> logger)
        {
            _logger = logger;
            var dbPath = GetDatabasePath();
            var connectionString = new ConnectionString
            {
                Filename = dbPath,
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

        private static string GetDatabasePath()
        {
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folderPath = Path.Combine(basePath, "Iris");
            Directory.CreateDirectory(folderPath);
            return Path.Combine(folderPath, DatabaseName);
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
