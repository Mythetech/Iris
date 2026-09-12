namespace Iris.Telemetry;

/// <summary>
/// Receives what the OTLP receiver produces. The desktop host adapts this onto the message bus;
/// tests record calls directly.
/// </summary>
public interface ITelemetrySink
{
    Task AcceptAsync(IReadOnlyList<ReceivedSpan> spans, CancellationToken cancellationToken);
    Task StatusChangedAsync(OtlpReceiverStatus status, string? endpoint, string? error, CancellationToken cancellationToken);
}
