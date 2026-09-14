using Iris.Desktop.Infrastructure;
using Iris.History;
using LiteDB;
using Microsoft.Extensions.Logging;

namespace Iris.Desktop.History;

public class HistoryRepository : IRepository
{
    private readonly IrisLiteDbContext _dbContext;
    private readonly HistorySettings _settings;
    private readonly ILogger<HistoryRepository> _logger;
    private bool _indexed;

    public string DbKey { get; } = "history";

    public HistoryRepository(IrisLiteDbContext dbContext, HistorySettings settings, ILogger<HistoryRepository> logger)
    {
        _dbContext = dbContext;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Every read and the retention sweep order by timestamp, and without an index LiteDB
    /// sorts the whole collection in memory to answer them. EnsureIndex is idempotent but
    /// still a round trip, so it runs once per context rather than once per call.
    /// </summary>
    public ILiteCollection<PersistentHistoryRecord> GetHistory()
    {
        var collection = _dbContext.GetCollection<PersistentHistoryRecord>(DbKey);

        if (!_indexed)
        {
            collection.EnsureIndex(x => x.Timestamp);
            _indexed = true;
        }

        return collection;
    }

    /// <summary>
    /// Paged and ordered in the query. This used to be FindAll() followed by an in-memory
    /// sort, so every caller paid for the entire history, and the callers that asked for
    /// ten rows were handed all of them.
    /// </summary>
    public List<HistoryRecord> GetHistoryRecords(int page = 1, int pageSize = 100)
    {
        var skip = (Math.Max(page, 1) - 1) * Math.Max(pageSize, 1);

        try
        {
            return GetHistory()
                .Query()
                .OrderByDescending(x => x.Timestamp)
                .Skip(skip)
                .Limit(Math.Max(pageSize, 1))
                .ToList()
                .Select(x => (HistoryRecord)x)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read history records from LiteDB");
            return [];
        }
    }

    public int Count() => GetHistory().Count();

    public void AddHistoryRecord(HistoryRecord record)
    {
        var persistent = PersistentHistoryRecord.FromHistoryRecord(record);
        GetHistory().Insert(persistent);

        Prune();
    }

    /// <summary>
    /// History grew without bound: one row per send, forever, in the same file as the
    /// user's connections and templates. Pruning on insert keeps the sweep proportional
    /// to what was just written rather than needing a background job.
    /// </summary>
    public int Prune()
    {
        var keep = _settings.MaxRecords;

        if (keep <= 0)
            return 0;

        var collection = GetHistory();
        var excess = collection.Count() - keep;

        if (excess <= 0)
            return 0;

        try
        {
            var oldest = collection
                .Query()
                .OrderBy(x => x.Timestamp)
                .Limit(excess)
                .ToList();

            foreach (var record in oldest)
                collection.Delete(record.Id);

            return oldest.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prune history down to {Keep} records", keep);
            return 0;
        }
    }

    public void DeleteHistory()
    {
        GetHistory().DeleteAll();
    }
}
