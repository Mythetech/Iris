using Iris.Brokers.Models;

namespace Iris.Brokers;

/// <summary>
/// Read-side contract for a broker connection. Implemented alongside
/// <see cref="IConnection"/> on broker classes that support peek/receive.
/// Call sites should probe with <c>connection is IMessageReader</c> or via
/// the helper extensions in <c>MessageReaderExtensions</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Semantics.</b> <c>ReceiveAsync</c> is destructive and irrevocable —
/// Iris uses auto-ack (ReceiveAndDelete) semantics across all brokers so
/// the service/UI layers don't have to care about lock tokens or
/// visibility timeouts. Native handles are retained on
/// <see cref="ReceivedMessage.Native"/> for diagnostic display only.
/// </para>
/// <para>
/// <b>Capabilities.</b> Brokers report honest capability flags via
/// <see cref="Capabilities"/>. Unsupported operations additionally throw
/// <see cref="NotSupportedException"/> as defense-in-depth for non-UI
/// callers.
/// </para>
/// </remarks>
public interface IMessageReader
{
    ReaderCapabilities Capabilities { get; }

    /// <summary>Non-destructively read a single message.</summary>
    Task<ReceivedMessage?> PeekAsync(
        EndpointDetails endpoint,
        ReadSource source = ReadSource.Main,
        CancellationToken cancellationToken = default);

    /// <summary>Non-destructively read up to <paramref name="count"/> messages.</summary>
    Task<IReadOnlyList<ReceivedMessage>> PeekAsync(
        EndpointDetails endpoint,
        int count,
        ReadSource source = ReadSource.Main,
        CancellationToken cancellationToken = default);

    /// <summary>Destructively consume a single message (auto-ack).</summary>
    Task<ReceivedMessage?> ReceiveAsync(
        EndpointDetails endpoint,
        ReadSource source = ReadSource.Main,
        CancellationToken cancellationToken = default);

    /// <summary>Destructively consume up to <paramref name="count"/> messages (auto-ack).</summary>
    Task<IReadOnlyList<ReceivedMessage>> ReceiveAsync(
        EndpointDetails endpoint,
        int count,
        ReadSource source = ReadSource.Main,
        CancellationToken cancellationToken = default);
}
