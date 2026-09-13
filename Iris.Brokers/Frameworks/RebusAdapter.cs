using Iris.Contracts.Messaging.Frameworks;
using RebusHeaders = Rebus.Messages.Headers;

namespace Iris.Brokers.Frameworks;

/// <summary>
/// Wraps a message in a Rebus-accurate envelope. Unlike MassTransit / NServiceBus / Wolverine,
/// Rebus does not put a JSON envelope object in the body — it puts metadata in transport headers
/// (the rbs2-* keys) and the raw user JSON in the body. This adapter therefore mutates
/// <see cref="IMessageRequest.Headers"/> in place and returns <see cref="IMessageRequest.Json"/>
/// unchanged. The headers flow through to the transport layer (e.g. RabbitMQ basic-properties)
/// so a default-configured Rebus consumer can deserialize the result.
/// </summary>
public class RebusAdapter : IFramework
{
    public string Name => "Rebus";

    public IReadOnlyList<FrameworkKey> Keys { get; } =
    [
        FrameworkKey.Header(RebusHeaders.MessageId, required: true),
        FrameworkKey.Header(RebusHeaders.Type, required: true),
        FrameworkKey.Header(RebusHeaders.ContentType, required: true),
        FrameworkKey.Header(RebusHeaders.CorrelationId),
        FrameworkKey.Header(RebusHeaders.CorrelationSequence),
        FrameworkKey.Header(RebusHeaders.SentTime),
        FrameworkKey.Header(RebusHeaders.ReturnAddress),
        FrameworkKey.Header(RebusHeaders.SenderAddress),
        FrameworkKey.Header(RebusHeaders.Intent),
    ];

    public IReadOnlySet<string> VerifiedProviders { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ConnectorProviders.RabbitMq };

    public FrameworkDescriptor Descriptor { get; } = new("Rebus",
    [
        CommonInputs.TypeName("the rbs2-msg-type header"),
        CommonInputs.AssemblyName("the assembly part of rbs2-msg-type; Rebus resolves it with Type.GetType"),
    ]);

    public string CreateWrappedMessage(IMessageRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Json);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MessageType);

        var messageId = Guid.NewGuid().ToString();
        var typeName = BuildRebusTypeName(request);

        // TryAdd everywhere — any header the caller already set wins.
        request.Headers.TryAdd(RebusHeaders.MessageId, messageId);
        request.Headers.TryAdd(RebusHeaders.CorrelationId, messageId);
        request.Headers.TryAdd(RebusHeaders.CorrelationSequence, "0");
        request.Headers.TryAdd(RebusHeaders.SentTime, DateTimeOffset.Now.ToString("O"));
        request.Headers.TryAdd(RebusHeaders.ReturnAddress, "iris");
        request.Headers.TryAdd(RebusHeaders.SenderAddress, "iris");
        request.Headers.TryAdd(RebusHeaders.Type, typeName);
        request.Headers.TryAdd(RebusHeaders.ContentType, "application/json;charset=utf-8");
        request.Headers.TryAdd(RebusHeaders.Intent, RebusHeaders.IntentOptions.PointToPoint);

        return request.Json;
    }

    private static string BuildRebusTypeName(IMessageRequest request)
    {
        var fqn = request.MessageFullyQualifiedName ?? request.MessageType;
        return string.IsNullOrWhiteSpace(request.MessageAssemblyName)
            ? fqn
            : $"{fqn}, {request.MessageAssemblyName}";
    }
}
