namespace Iris.Brokers.Frameworks;

/// <summary>
/// Wraps a message in a Paramore.Brighter v10-accurate envelope. Brighter does not put a JSON
/// envelope object in the body — it puts metadata in transport headers (CloudEvents 1.0
/// attributes plus a small set of Brighter-native keys) and the raw user JSON in the body.
/// This adapter therefore mutates <see cref="IMessageRequest.Headers"/> in place and returns
/// <see cref="IMessageRequest.Json"/> unchanged. The headers flow through to the transport
/// layer (e.g. RabbitMQ application headers, Azure Service Bus application properties) so a
/// default-configured Brighter consumer can deserialize the result.
/// </summary>
/// <remarks>
/// Header key spelling matches <c>Paramore.Brighter.MessagingGateway.RMQ.Async/HeaderNames.cs</c>
/// from Brighter v10.3.3 — note <c>cloudEvents_*</c> (camelCase E), not <c>ce_*</c>. We hard-code
/// the strings rather than reference Paramore.Brighter so that Iris.Brokers does not pull in a
/// transport pipeline it never hosts.
/// </remarks>
public class BrighterAdapter : IFramework
{
    public string Name => "Brighter";

    public string CreateWrappedMessage(IMessageRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Json);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MessageType);

        var messageId = Guid.NewGuid().ToString();
        var typeName = request.MessageFullyQualifiedName ?? request.MessageType;

        var topic = request.Properties.TryGetValue("topic", out var t) && !string.IsNullOrWhiteSpace(t)
            ? t
            : request.MessageType;

        // Iris does not yet model command-vs-event semantics on IMessageRequest. Default to
        // MT_EVENT (the common pub/sub case Brighter shops use); allow callers to override via
        // Properties["BrighterMessageType"] for MT_COMMAND, MT_DOCUMENT, etc.
        var brighterMessageType = request.Properties.TryGetValue("BrighterMessageType", out var mt)
                                  && !string.IsNullOrWhiteSpace(mt)
            ? mt
            : "MT_EVENT";

        // Brighter-native headers.
        request.Headers["MessageType"] = brighterMessageType;
        request.Headers["MessageId"] = messageId;
        request.Headers["Topic"] = topic;
        request.Headers["HandledCount"] = "0";
        request.Headers["CorrelationId"] = request.Headers.TryGetValue("CorrelationId", out var cid)
            ? cid
            : messageId;

        // CloudEvents 1.0 attributes — Brighter v10 reads these from the AMQP application
        // headers / Azure SB application properties.
        request.Headers["cloudEvents_id"] = messageId;
        request.Headers["cloudEvents_specversion"] = "1.0";
        request.Headers["cloudEvents_type"] = typeName;
        request.Headers["cloudEvents_source"] = "iris://broker";
        request.Headers["cloudEvents_time"] = DateTimeOffset.UtcNow.ToString("O");

        // Body is the raw POCO JSON, identical to Rebus / EasyNetQ.
        return request.Json;
    }
}
