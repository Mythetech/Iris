using System.Text.Json;
using Iris.Components.History;
using Iris.Components.Infrastructure.MessageBus;
using Iris.Contracts.Audit;
using Iris.Contracts.Messaging.Events;
using Iris.Desktop.Infrastructure;
using Iris.History;
using Microsoft.AspNetCore.Components.Authorization;

namespace Iris.Desktop.History;

public class MessageRecorder : IConsumer<MessageSent>
{
    private readonly HistoryState _state;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly HistoryRepository _db;
    private LocalHistoryService LocalHistoryService { get; }
    public MessageRecorder(HistoryState state, AuthenticationStateProvider authenticationStateProvider, HistoryRepository db)
    {
        _state = state;
        _authenticationStateProvider = authenticationStateProvider;
        _db = db;
    }
    public async Task Consume(MessageSent message)
    {
        var auth = await _authenticationStateProvider.GetAuthenticationStateAsync();

        bool anonymous = !auth.User.Identity.IsAuthenticated;
        
        string source = anonymous ? "Local" : auth.User.Identity?.Name ?? "Unknown";
        
        var record = new HistoryRecord(Actions.MessageSent, source)
        {
            Action = Actions.MessageSent,
            Details = JsonSerializer.Serialize(ToDetailsDictionary(message)),
            EventAction = "SendMessage",
            Target = message.Address,
            Timestamp = DateTimeOffset.Now,
            Source = source
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