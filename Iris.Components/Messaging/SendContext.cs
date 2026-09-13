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

    /// <summary>
    /// Framework for this send. When null the orchestrator falls back to the
    /// ambient <see cref="MessageState.SelectedFramework"/>. Callers that compose
    /// outside the Messaging page supply this so they never mutate shared state.
    /// </summary>
    public string? Framework { get; init; }

    /// <summary>
    /// Headers for this send. When null the orchestrator falls back to the ambient
    /// <see cref="MessageState.Headers"/>.
    /// </summary>
    public Dictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// Repeat count for this send. When null the orchestrator falls back to the
    /// ambient <see cref="MessageState.Repeat"/>.
    /// </summary>
    public int? Repeat { get; init; }

    /// <summary>
    /// Delay in seconds before each send. When null the orchestrator falls back to
    /// the ambient <see cref="MessageState.Delay"/>.
    /// </summary>
    public int? Delay { get; init; }

    /// <summary>
    /// True when this context is fully self-contained and the orchestrator must not
    /// write to the ambient <see cref="MessageState"/> (for example, recording
    /// endpoint metadata). Callers that compose outside the Messaging page, like the
    /// quick-send panel, set this so a drawer send never mutates state the page is
    /// still using.
    /// </summary>
    public bool IsolateFromMessageState { get; init; }
}
