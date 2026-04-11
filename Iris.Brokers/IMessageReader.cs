using Iris.Brokers.Models;

namespace Iris.Brokers;

/// <summary>
/// Marker base for broker connections that expose any read capability.
/// Not useful on its own — probe for one of the operation-specific
/// sub-interfaces (<see cref="IMessagePeeker"/>, <see cref="IMessageReceiver"/>,
/// <see cref="IDeadLetterPeeker"/>, <see cref="IDeadLetterReceiver"/>).
/// </summary>
/// <remarks>
/// <para>
/// Each read capability is its own interface. A broker implements only
/// those it actually supports. Callers pattern-match on the specific
/// interface they need (<c>connection is IMessagePeeker peeker</c>).
/// This encodes broker capabilities in the type system instead of as
/// runtime flags, avoiding LSP-violating <c>NotSupportedException</c>
/// throws on opted-out operations.
/// </para>
/// <para>
/// <b>Semantics.</b> <c>Receive</c> methods are destructive and
/// irrevocable — Iris uses auto-ack (ReceiveAndDelete) semantics across
/// all brokers so the service/UI layers don't have to care about lock
/// tokens or visibility timeouts. Native per-broker handles are retained
/// on <see cref="ReceivedMessage.Native"/> for diagnostic display only.
/// </para>
/// </remarks>
public interface IMessageReader { }

/// <summary>Non-destructive read from the main queue.</summary>
public interface IMessagePeeker : IMessageReader
{
    /// <summary>Upper bound on <c>count</c> the broker will honor in a single call.</summary>
    int MaxPeekBatchSize { get; }

    /// <summary>Non-destructively read up to <paramref name="count"/> messages from the main queue.</summary>
    Task<IReadOnlyList<ReceivedMessage>> PeekAsync(
        EndpointDetails endpoint,
        int count,
        CancellationToken cancellationToken = default);
}

/// <summary>Destructive read from the main queue (auto-ack).</summary>
public interface IMessageReceiver : IMessageReader
{
    /// <summary>Upper bound on <c>count</c> the broker will honor in a single call.</summary>
    int MaxReceiveBatchSize { get; }

    /// <summary>Destructively consume up to <paramref name="count"/> messages from the main queue.</summary>
    Task<IReadOnlyList<ReceivedMessage>> ReceiveAsync(
        EndpointDetails endpoint,
        int count,
        CancellationToken cancellationToken = default);
}

/// <summary>Non-destructive read from the broker's dead-letter sub-queue.</summary>
public interface IDeadLetterPeeker : IMessageReader
{
    /// <summary>Non-destructively read up to <paramref name="count"/> messages from the dead-letter sub-queue.</summary>
    Task<IReadOnlyList<ReceivedMessage>> PeekDeadLetterAsync(
        EndpointDetails endpoint,
        int count,
        CancellationToken cancellationToken = default);
}

/// <summary>Destructive read from the broker's dead-letter sub-queue (auto-ack).</summary>
public interface IDeadLetterReceiver : IMessageReader
{
    /// <summary>Destructively consume up to <paramref name="count"/> messages from the dead-letter sub-queue.</summary>
    Task<IReadOnlyList<ReceivedMessage>> ReceiveDeadLetterAsync(
        EndpointDetails endpoint,
        int count,
        CancellationToken cancellationToken = default);
}
