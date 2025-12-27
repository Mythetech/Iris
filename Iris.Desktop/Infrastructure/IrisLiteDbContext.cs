using LiteDB;
using System.Security.Cryptography;

namespace Iris.Desktop.Infrastructure
{
    public class IrisLiteDbContext : IDisposable
    {
        private static string DatabaseName { get; } = "IrisDb.db";
        private readonly LiteDatabase _database;
        private bool _disposed;

        public IrisLiteDbContext()
        {
            var dbPath = GetDatabasePath();
            var connectionString = new ConnectionString
            {
                Filename = dbPath,
                Connection = ConnectionType.Shared
            };

            connectionString.Password = GetEncryptionKey();

            _database = new LiteDatabase(connectionString);
        }

        private static string GetDatabasePath()
        {
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folderPath = Path.Combine(basePath, "Iris");
            Directory.CreateDirectory(folderPath);
            return Path.Combine(folderPath, DatabaseName);
        }

        private static string GetEncryptionKey()
        {
            var machineIdentifier = $"{Environment.MachineName}:{Environment.UserName}:Iris";
            return Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(machineIdentifier)));
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