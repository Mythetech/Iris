using System.Text.Json;
using Wolverine;

namespace Iris.Brokers.Frameworks;

public class WolverineAdapter : IFramework
{
    public string Name { get; } = "Wolverine";
    
    public string CreateWrappedMessage(IMessageRequest request)
    {
        var envelope = new Envelope(request.Json);

        foreach (var header in request.Headers)
        {
            envelope.Headers.TryAdd(header.Key, header.Value);
        }
        
        envelope.MessageType = request.MessageType;
        
        return JsonSerializer.Serialize(envelope);
    }
}