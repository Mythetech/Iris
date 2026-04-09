using Iris.Desktop.Infrastructure;
using Iris.History;
using LiteDB;
using Microsoft.Extensions.Logging;

namespace Iris.Desktop.History;

public class HistoryRepository : IRepository
{
    private readonly IrisLiteDbContext _dbContext;
    private readonly ILogger<HistoryRepository> _logger;

    public string DbKey { get; } = "history";

    public HistoryRepository(IrisLiteDbContext dbContext, ILogger<HistoryRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public ILiteCollection<PersistentHistoryRecord> GetHistory()
    {
        return _dbContext.GetCollection<PersistentHistoryRecord>(DbKey);
    }

    public List<HistoryRecord> GetHistoryRecords()
    {
        try
        {
            return GetHistory()
                .FindAll()
                .Select(x => (HistoryRecord)x)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read history records from LiteDB");
            return [];
        }
    }

    public void AddHistoryRecord(HistoryRecord record)
    {
        var persistent = PersistentHistoryRecord.FromHistoryRecord(record);
        GetHistory().Insert(persistent);
    }

    public void DeleteHistory()
    {
        GetHistory().DeleteAll();
    }

}