using Iris.Desktop.Infrastructure;
using Iris.History;
using LiteDB;

namespace Iris.Desktop.History;

public class HistoryRepository : IRepository
{
    private readonly IrisLiteDbContext _dbContext;
    
    public string DbKey { get; } = "history";

    public HistoryRepository(IrisLiteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public ILiteCollection<PersistentHistoryRecord> GetHistory()
    {
        return _dbContext.GetCollection<PersistentHistoryRecord>(DbKey);
    }
    
    public List<HistoryRecord> GetHistoryRecords()
    {
        return GetHistory()
            .FindAll()
            .Select(x => (HistoryRecord)x)
            .ToList();
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