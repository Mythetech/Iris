using Iris.Contracts.Results;

namespace Iris.Components.Messaging;

/// <summary>
/// Orchestrates a send operation: applies ambient <see cref="MessageState"/> for
/// delay / repeat / framework / headers / properties, invokes
/// <see cref="IMessageService.SendMessageAsync(string, string, string?, string?, System.Collections.Generic.Dictionary{string,string}?, System.Collections.Generic.Dictionary{string,string}?)"/>,
/// and updates progress state. Lives in Iris.Components so both /Messaging and the
/// per-connection Send view call the same pipeline.
/// </summary>
public interface IMessageSendOrchestrator
{
    /// <summary>
    /// Sends a message described by <paramref name="context"/>. Respects
    /// <see cref="MessageState.Delay"/> and <see cref="MessageState.Repeat"/>
    /// when non-zero; callers that don't surface delay/repeat UI simply leave
    /// those at their defaults.
    /// </summary>
    /// <param name="context">Send input (json, provider, endpoint).</param>
    /// <param name="progress">
    /// Optional per-iteration callback. When <see cref="MessageState.Repeat"/> is
    /// greater than zero the orchestrator reports each individual send result
    /// (Repeat + 1 total reports), letting callers surface every success and
    /// failure as it happens. When Repeat is zero the callback fires exactly once
    /// with the single send result.
    /// </param>
    /// <param name="cancellationToken">Cancellation token forwarded to delay waits.</param>
    /// <returns>
    /// The result of the LAST send call. Mostly useful when no <paramref name="progress"/>
    /// callback is supplied. Callers that subscribe to <paramref name="progress"/> can
    /// safely ignore this return value.
    /// </returns>
    Task<Result<bool>> SendAsync(
        SendContext context,
        IProgress<Result<bool>>? progress = null,
        CancellationToken cancellationToken = default);
}
