using Iris.Desktop.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Iris.Desktop.Brokers;

public class ConnectionRepository
{
    private readonly IrisLiteDbContext _dbContext;
    private readonly ILogger<ConnectionRepository> _logger;

    private const string DbKey = "connections";

    public ConnectionRepository(IrisLiteDbContext dbContext, ILogger<ConnectionRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public List<SavedConnection> GetAll()
    {
        try
        {
            return _dbContext.GetCollection<SavedConnection>(DbKey)
                .FindAll()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read saved connections from LiteDB");
            return [];
        }
    }

    public void Save(SavedConnection connection)
    {
        var collection = _dbContext.GetCollection<SavedConnection>(DbKey);

        var existing = collection.FindOne(x => x.Address == connection.Address);

        if (existing != null)
        {
            connection.Id = existing.Id;
            collection.Update(connection);
        }
        else
        {
            collection.Insert(connection);
        }
    }

    public void Delete(string address)
    {
        var collection = _dbContext.GetCollection<SavedConnection>(DbKey);
        collection.DeleteMany(x => x.Address == address);
    }
}
