
using System.Text.Json;
using Iris.Contracts.Audit.Models;

namespace Iris.Components.History;

public class HistoryState
{
    private readonly IHistoryService _service;
    public List<HistoryRecord>? History { get; private set; }
    
    private List<AuditRecord>? _auditRecords;

    public HistoryState(IHistoryService service)
    {
        _service = service;
    }

    public event Action? OnHistoryStateChange;
    
    private void NotifyHistoryStateChanged() => OnHistoryStateChange?.Invoke();

    public void Refresh()
    {
        History = null;
    }
    
    public async Task<List<AuditRecord>> GetUserHistoryAsync(int page = 1, int pageSize = 100)
    {
        if (History == null || History?.Count < 1)
        {
            _auditRecords = await _service.GetUserHistoryAsync(page, 1000);
            History = _auditRecords
                .Select(x => new HistoryRecord()
                {
                    Action = x.Action,
                    Details = JsonSerializer.Serialize(x.Details),
                    EventAction = x.Action,
                    EventParameters = x.Details.ToDictionary(k => k.Key, object (v) => v.Value),
                    Source = x.User,
                    Target = x.Target,
                    Timestamp = x.When.GetValueOrDefault()
                })
                .ToList();
        }
        
        NotifyHistoryStateChanged();
        
        return _auditRecords;
    }

    public async Task AddHistoryRecord(HistoryRecord record)
    {
        if (History == null)
        {
            await GetUserHistoryAsync();
        }
        
        History.Add(record);
        
        NotifyHistoryStateChanged();
    }
}