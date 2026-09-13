using Iris.Brokers.Models;
using Iris.Contracts.Messaging.Frameworks;

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
    public const string TopicKey = "Topic";
    public const string MessageKindKey = "BrighterMessageType";

    public string Name => "Brighter";

    public IReadOnlyList<FrameworkKey> Keys { get; } =
    [
        FrameworkKey.Header("MessageType", required: true),
        FrameworkKey.Header("MessageId"),
        FrameworkKey.Header("Topic"),
        FrameworkKey.Header("HandledCount", HeaderDataType.Integer),
        FrameworkKey.Header("CorrelationId"),
        FrameworkKey.Header("cloudEvents_id"),
        FrameworkKey.Header("cloudEvents_specversion"),
        FrameworkKey.Header("cloudEvents_type"),
        FrameworkKey.Header("cloudEvents_source"),
        FrameworkKey.Header("cloudEvents_time", HeaderDataType.Timestamp),
        FrameworkKey.Transport(TransportProperty.MessageId),
        FrameworkKey.Transport(TransportProperty.ContentType),
        FrameworkKey.Transport(TransportProperty.Type),
        FrameworkKey.Transport(TransportProperty.Timestamp),
    ];

    public IReadOnlySet<string> VerifiedProviders { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ConnectorProviders.RabbitMq };

    public FrameworkDescriptor Descriptor { get; } = new("Brighter",
    [
        CommonInputs.TypeName("the cloudEvents_type header"),
        new FrameworkInput(TopicKey, "Topic", "Brighter's Topic header. Blank means the endpoint name."),
        new FrameworkInput(MessageKindKey, "Message kind", "Brighter's MessageType header: MT_EVENT for pub/sub, MT_COMMAND for point-to-point.",
            DefaultValue: "MT_EVENT", AllowedValues: ["MT_EVENT", "MT_COMMAND", "MT_DOCUMENT"]),
    ]);

    public string CreateWrappedMessage(IMessageRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Json);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MessageType);

        var messageId = Guid.NewGuid().ToString();
        var typeName = request.MessageFullyQualifiedName ?? request.MessageType;

        var topic = request.Properties.TryGetValue(TopicKey, out var t) && !string.IsNullOrWhiteSpace(t)
            ? t
            : request.MessageType;

        // Iris does not yet model command-vs-event semantics on IMessageRequest. Default to
        // MT_EVENT (the common pub/sub case Brighter shops use); allow callers to override via
        // Properties[MessageKindKey] for MT_COMMAND, MT_DOCUMENT, etc.
        var brighterMessageType = request.Properties.TryGetValue(MessageKindKey, out var mt)
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

        // RmqMessageCreator reads the id, the body content type (from the AMQP `type`
        // property) and the timestamp from basic properties, not from headers.
        var timestamp = DateTimeOffset.UtcNow;
        request.Headers["cloudEvents_time"] = timestamp.ToString("O");
        request.TransportProperties.MessageId = messageId;
        request.TransportProperties.ContentType = "application/json";
        request.TransportProperties.Type = "application/json";
        request.TransportProperties.Timestamp = timestamp;

        // Body is the raw POCO JSON, identical to Rebus / EasyNetQ.
        return request.Json;
    }
}
