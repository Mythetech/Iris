namespace Iris.Brokers.Models;

/// <summary>
/// A message read off a broker. Shape is broker-agnostic; fields that a
/// given broker does not supply are left null. Native per-broker metadata
/// (lock tokens, receipt handles, etc.) is retained on <see cref="Native"/>
/// for diagnostic display only.
/// </summary>
public sealed class ReceivedMessage
{
    /// <summary>Payload as a string — JSON in the common case, raw text otherwise.</summary>
    public required string Body { get; init; }

    public string? MessageId { get; init; }

    public string? CorrelationId { get; init; }

    public string? ContentType { get; init; }

    /// <summary>Transport-level headers (AMQP basic properties, SB application properties, SQS attributes).</summary>
    public Dictionary<string, string> Headers { get; init; } = new();

    /// <summary>Application-level properties distinct from transport headers.</summary>
    public Dictionary<string, string> Properties { get; init; } = new();

    /// <summary>How many times this message has been delivered, if the broker reports it.</summary>
    public int? DeliveryCount { get; init; }

    /// <summary>When the message was enqueued, if the broker reports it.</summary>
    public DateTimeOffset? EnqueuedTimeUtc { get; init; }

    /// <summary>When the message will expire, if the broker reports it.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; init; }

    /// <summary>Payload size in bytes, if the broker reports it.</summary>
    public long? SizeInBytes { get; init; }

    /// <summary>Which sub-queue the message was read from.</summary>
    public ReadSource Source { get; init; }

    /// <summary>Provider identifier, e.g. "RabbitMQ", "AzureServiceBus", "AzureQueueStorage", "AmazonSQS".</summary>
    public string Provider { get; init; } = "";

    /// <summary>Opaque per-broker metadata for diagnostic display.</summary>
    public NativeMessageMetadata? Native { get; init; }
}
