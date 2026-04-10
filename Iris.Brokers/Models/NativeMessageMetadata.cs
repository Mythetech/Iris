namespace Iris.Brokers.Models;

/// <summary>
/// Provider-specific diagnostic metadata retained on a received message.
/// These values are informational only — Iris uses <c>ReceiveAndDelete</c>
/// semantics so lock tokens and receipt handles are not used for ack flow.
/// </summary>
public sealed class NativeMessageMetadata
{
    /// <summary>Azure Service Bus lock token.</summary>
    public string? LockToken { get; init; }

    /// <summary>Amazon SQS receipt handle.</summary>
    public string? ReceiptHandle { get; init; }

    /// <summary>Azure Queue Storage pop receipt.</summary>
    public string? PopReceipt { get; init; }

    /// <summary>RabbitMQ routing key.</summary>
    public string? RoutingKey { get; init; }

    /// <summary>RabbitMQ exchange the message was delivered through.</summary>
    public string? Exchange { get; init; }

    /// <summary>Anything else the provider wants to surface for diagnostics.</summary>
    public Dictionary<string, string> Extra { get; init; } = new();
}
