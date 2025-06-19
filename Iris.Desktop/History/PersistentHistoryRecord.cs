using Iris.Desktop.Infrastructure;
using Iris.History;

namespace Iris.Desktop.History;

public class PersistentHistoryRecord : HistoryRecord, ILocalEntity
{
    public PersistentHistoryRecord(string action, string source) : base(action, source)
    {
    }

    public PersistentHistoryRecord(string action, string source, string target) : base(action, source, target)
    {
    }

    public int Id { get; set; }
    
    public static PersistentHistoryRecord FromHistoryRecord(HistoryRecord record)
    {
        var persistentRecord = new PersistentHistoryRecord(record.Action, record.Source, record.Target)
        {
            Action = record.Action,
            EventAction = record.EventAction,
            EventParameters = record.EventParameters,
            Details = record.Details,
            Timestamp = record.Timestamp,
            Source = record.Source,
        };
        return persistentRecord;
    }
}