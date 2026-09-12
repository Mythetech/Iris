using Iris.Telemetry;

namespace Iris.Telemetry.Test;

/// <summary>
/// A sink whose StatusChangedAsync always throws, used to prove the host does not let a
/// misbehaving sink take down its own start or dispose lifecycle.
/// </summary>
public sealed class ThrowingStatusChangedSink : ITelemetrySink
{
    public Task AcceptAsync(IReadOnlyList<ReceivedSpan> spans, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StatusChangedAsync(OtlpReceiverStatus status, string? endpoint, string? error, CancellationToken cancellationToken)
        => throw new InvalidOperationException("sink exploded");
}
