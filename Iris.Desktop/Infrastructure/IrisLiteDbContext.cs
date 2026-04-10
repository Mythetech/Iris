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
                _logger.LogWarning(ex, "LiteDB database at {Path} is encrypted or corrupted. Recreating database.", dbPath);
                _database?.Dispose();

                try { File.Delete(dbPath); }
                catch (IOException deleteEx)
                {
                    _logger.LogError(deleteEx, "Failed to delete encrypted database at {Path}", dbPath);
                    throw;
                }

                _database = new LiteDatabase(connectionString);
            }
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
