namespace Iris.Contracts.Messaging.Models;

/// <summary>
/// Contract-layer representation of a message read from a broker. Mirrors
/// <c>Iris.Brokers.Models.ReceivedMessage</c> without pulling the broker
/// assembly into <c>Iris.Components</c> or UI projects.
/// </summary>
public sealed class ReceivedMessageDto
{
    public required string Body { get; init; }

    public string? MessageId { get; init; }

    public string? CorrelationId { get; init; }

    public string? ContentType { get; init; }

    public Dictionary<string, string> Headers { get; init; } = new();

    public Dictionary<string, string> Properties { get; init; } = new();

    public int? DeliveryCount { get; init; }

    public DateTimeOffset? EnqueuedTimeUtc { get; init; }

    public DateTimeOffset? ExpiresAtUtc { get; init; }

    public long? SizeInBytes { get; init; }

    public ReadSource Source { get; init; }

    public string Provider { get; init; } = "";

    /// <summary>
    /// Provider-specific diagnostic metadata (lock tokens, receipt handles,
    /// routing keys, etc.). Display-only.
    /// </summary>
    public Dictionary<string, string> Native { get; init; } = new();
}
