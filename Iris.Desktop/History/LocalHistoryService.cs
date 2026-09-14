using System.Text.Json;
using Iris.Components.History;
using Iris.Contracts.Audit;
using Iris.Contracts.Audit.Models;
using Iris.Desktop.Infrastructure;
using Iris.History;
using Microsoft.Extensions.Logging;
using HistoryRecord = Iris.History.HistoryRecord;

namespace Iris.Desktop.History;

public class LocalHistoryService : IHistoryService
{
    private readonly HistoryRepository _db;
    private readonly ILogger<LocalHistoryService> _logger;

    public LocalHistoryService(HistoryRepository db, ILogger<LocalHistoryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Both arguments used to be accepted and ignored: the repository returned everything
    /// and this sorted it in memory. HistoryPanel asks for ten rows and was handed the
    /// entire history on every render.
    /// </summary>
    public Task<List<AuditRecord>> GetUserHistoryAsync(int page = 1, int pageSize = 100)
    {
        var history = _db.GetHistoryRecords(page, pageSize);

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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize history record details");
                return null;
            }
        
        // Already ordered newest first by the query, so there is nothing to re-sort here.
        }).Where(r => r != null).ToList());
    }
    
}