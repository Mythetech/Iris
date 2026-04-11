using Iris.Contracts.Brokers.Models;

namespace Iris.Components.Messaging;

/// <summary>
/// Input to <see cref="IMessageSendOrchestrator.SendAsync"/>. All fields are optional
/// except Json — the orchestrator tolerates null provider/endpoint values the same
/// way the existing Messaging page does (the call to IMessageService will fail and
/// HandleResult will surface the error).
/// </summary>
public sealed class SendContext
{
    public required string Json { get; init; }
    public Provider? Provider { get; init; }
    public EndpointDetails? Endpoint { get; init; }

    /// <summary>
    /// If set, overrides the MessageState framework-property "MessageType" override
    /// fallback, and overrides <see cref="EndpointDetails.Name"/> as the effective
    /// message type. Usually left null — the orchestrator will derive the message
    /// type automatically.
    /// </summary>
    public string? MessageTypeOverride { get; init; }
}
