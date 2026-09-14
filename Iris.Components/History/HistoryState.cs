
using System.Text.Json;
using Iris.Contracts.Audit.Models;

namespace Iris.Components.History;

public class HistoryState
{
    private readonly IHistoryService _service;

    /// <summary>
    /// The cached window, replaced wholesale rather than mutated. The History grid and the
    /// Recent tab enumerate this while rendering and <c>MessageRecorder</c> writes to it from
    /// the send path, so an in-place add is an InvalidOperationException in whichever reader
    /// happens to be mid-enumeration.
    /// </summary>
    public List<HistoryRecord>? History { get; private set; }

    private readonly Lock _sync = new();

    private List<AuditRecord>? _auditRecords;

    public HistoryState(IHistoryService service)
    {
        _service = service;
    }

    public event Action? OnHistoryStateChange;
    
    private void NotifyHistoryStateChanged() => OnHistoryStateChange?.Invoke();

    /// <summary>
    /// How much history the cached window holds. The History grid pages client-side over
    /// whatever is cached here, so this is the ceiling on what it can show, not a page
    /// size in the storage sense.
    /// </summary>
    public const int DefaultPageSize = 1000;

    public void Refresh()
    {
        History = null;
        _auditRecords = null;

        // Notified, because dropping the cache is a visible change. Clearing history from
        // the settings panel left both the grid and the Recent tab showing records that
        // were already deleted until something else happened to trigger a render.
        NotifyHistoryStateChanged();
    }

    public async Task<List<AuditRecord>> GetUserHistoryAsync(int page = 1, int pageSize = DefaultPageSize)
    {
        if (History == null || History?.Count < 1)
        {
            // pageSize used to be discarded here in favour of a hardcoded 1000, so a
            // caller asking for ten rows loaded a thousand.
            _auditRecords = await _service.GetUserHistoryAsync(page, pageSize);
            History = _auditRecords
                .Select(x => new HistoryRecord()
                {
                    Action = x.Action,
                    Details = JsonSerializer.Serialize(x.Details),
                    EventAction = x.Action,
                    // Details is optional on the contract, and the local service produces a
                    // null one from a row whose stored details deserialize to null.
                    // Flattening it unguarded took out the whole page, not just the row.
                    EventParameters = x.Details?.ToDictionary(k => k.Key, object (v) => v.Value) ?? [],
                    Source = x.User,
                    Target = x.Target,
                    Timestamp = x.When.GetValueOrDefault()
                })
                .ToList();
        }
        
        NotifyHistoryStateChanged();
        
        return _auditRecords ?? [];
    }

    public async Task AddHistoryRecord(HistoryRecord record)
    {
        if (History == null)
        {
            await GetUserHistoryAsync();
        }

        lock (_sync)
        {
            History = [.. History ?? [], record];
        }

        NotifyHistoryStateChanged();
    }
}