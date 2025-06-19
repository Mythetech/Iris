namespace Iris.History;

public class HistoryRecord
{
    public required string Action { get; set; }
    
    public string? EventAction { get; set; }
    
    public Dictionary<string, object>? EventParameters { get; set; }
    
    public required string? Source { get; set; }
    
    public string? Target { get; set; }
    
    public string? Details { get; set; }
    
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

    public HistoryRecord(string action, string source)
    {
        Action = action;
        Source = source;
    }

    public HistoryRecord(string action, string source, string target)
    {
        Action = action;
        Source = source;
        Target = target;
    }

    public Iris.Contracts.Audit.Models.HistoryRecord ToContract()
    {
        return new Iris.Contracts.Audit.Models.HistoryRecord
        {
            Action = Action,
            Source = Source,
            Target = Target,
            EventAction = EventAction,
            EventParameters = EventParameters,
            Timestamp = Timestamp,
            Details = Details,
        };
    }

    public static HistoryRecord FromContract(Iris.Contracts.Audit.Models.HistoryRecord record)
    {
        return new HistoryRecord(record.Action, record.Source, record.Target)
        {
            Action = record.Action,
            EventAction = record.EventAction,
            Details = record.Details,
            EventParameters = record.EventParameters,
            Source = record.Source,
            Timestamp = record.Timestamp
        };
    }
}