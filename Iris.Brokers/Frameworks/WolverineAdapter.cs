using System.Globalization;
using Iris.Brokers.Models;
using Iris.Contracts.Messaging.Frameworks;

namespace Iris.Brokers.Frameworks;

/// <summary>
/// Wolverine's RabbitMQ mapper reads the message identity from the AMQP <c>type</c>
/// property, the id from <c>message_id</c>, the correlation id and content type from
/// their basic properties, and every other envelope field from application headers.
/// The body is the raw message JSON. Handlers are keyed by the type's full name unless
/// the type carries a <c>[MessageIdentity]</c> alias, in which case the caller supplies
/// that alias as the type name. Mirrors <c>RabbitMqEnvelopeMapper</c> and
/// <c>EnvelopeMapper</c> at Wolverine V5.29.0.
/// </summary>
public class WolverineAdapter : IFramework
{
    public const string ProtocolVersionHeader = "wolverine-protocol-version";
    public const string SourceHeader = "source";
    public const string SentAtHeader = "sent-at";
    public const string ConversationIdHeader = "conversation-id";

    private const string SentAtFormat = "yyyy-MM-dd HH:mm:ss:ffffff Z";

    public string Name => "Wolverine";

    public IReadOnlyList<FrameworkKey> Keys { get; } =
    [
        FrameworkKey.Transport(TransportProperty.Type, required: true),
        FrameworkKey.Transport(TransportProperty.ContentType),
        FrameworkKey.Transport(TransportProperty.MessageId),
        FrameworkKey.Transport(TransportProperty.CorrelationId),
        FrameworkKey.Header(ProtocolVersionHeader),
        FrameworkKey.Header(SourceHeader),
        FrameworkKey.Header(SentAtHeader),
        FrameworkKey.Header(ConversationIdHeader),
    ];

    public IReadOnlySet<string> VerifiedProviders { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ConnectorProviders.RabbitMq };

    public FrameworkDescriptor Descriptor { get; } = new("Wolverine",
    [
        CommonInputs.TypeName("the AMQP type property; Wolverine keys handlers by the full type name, or by the [MessageIdentity] alias when the type has one, so type the alias here in that case"),
    ]);

    public string CreateWrappedMessage(IMessageRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Json);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MessageType);

        var identity = request.MessageFullyQualifiedName ?? request.MessageType;

        var messageId = Guid.NewGuid().ToString();
        var transport = request.TransportProperties;

        transport.Type = identity;
        transport.ContentType = "application/json";
        transport.MessageId = messageId;
        transport.CorrelationId ??= messageId;

        request.Headers.TryAdd(ProtocolVersionHeader, "1.0");
        request.Headers.TryAdd(SourceHeader, "iris");
        request.Headers.TryAdd(SentAtHeader, DateTimeOffset.UtcNow.ToString(SentAtFormat, CultureInfo.InvariantCulture));
        request.Headers.TryAdd(ConversationIdHeader, messageId);

        return request.Json;
    }
}
