using System.Text.Json;
using Iris.Components.History;
using Mythetech.Framework.Infrastructure.MessageBus;
using Iris.Contracts.Audit;
using Iris.Contracts.Messaging.Events;
using Iris.Desktop.Infrastructure;
using Iris.History;

namespace Iris.Desktop.History;

public class MessageRecorder : IConsumer<MessageSent>
{
    private readonly HistoryState _state;
    private readonly HistoryRepository _db;
    public MessageRecorder(HistoryState state, HistoryRepository db)
    {
        _state = state;
        _db = db;
    }
    public async Task Consume(MessageSent message)
    {
        var record = new HistoryRecord(Actions.MessageSent, "Local")
        {
            Action = Actions.MessageSent,
            Details = JsonSerializer.Serialize(ToDetailsDictionary(message)),
            EventAction = "SendMessage",
            Target = message.Address,
            Timestamp = DateTimeOffset.Now,
            Source = "Local"
        };
        
        _db.AddHistoryRecord(record);
        
        await _state.AddHistoryRecord(record.ToContract());
    }

    private Dictionary<string, string> ToDetailsDictionary(MessageSent message)
    {
        var details = new Dictionary<string, string>
        {
            ["Message"] = message.Message,
            ["Address"] = message.Address,
            ["Provider"] = message.Provider,
            ["Endpoint"] = message?.Endpoint ?? "--",
            ["Headers"] = JsonSerializer.Serialize(message.Headers),
            ["Properties"] = JsonSerializer.Serialize(message.Properties)
        };

        return details;
    }
}