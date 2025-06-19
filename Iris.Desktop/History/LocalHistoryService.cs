using System.Text.Json;
using Iris.Components.History;
using Iris.Contracts.Audit;
using Iris.Contracts.Audit.Models;
using Iris.Desktop.Infrastructure;
using Iris.History;
using HistoryRecord = Iris.History.HistoryRecord;

namespace Iris.Desktop.History;

public class LocalHistoryService : IHistoryService
{
    private readonly HistoryRepository _db;
    
    public LocalHistoryService(HistoryRepository db)
    {
        _db = db;
    }

    private List<HistoryRecord> History { get; } = new();
    
    public Task<List<AuditRecord>> GetUserHistoryAsync(int page = 1, int pageSize = 100)
    {
        var history = _db.GetHistoryRecords();

        return Task.FromResult(history.Select(x =>
        {
            try
            {
                return new AuditRecord()
                {
                    Action = x.Action,
                    Target = x?.Target,
                    Details = JsonSerializer.Deserialize<Dictionary<string, string>>(x?.Details),
                    When = x.Timestamp,
                    User = x.Source,
                };
            }
            catch (Exception)
            {
                return null;
            }
        
        }).Where(r => r != null).OrderByDescending(x => x?.When).ToList());
    }
    
}