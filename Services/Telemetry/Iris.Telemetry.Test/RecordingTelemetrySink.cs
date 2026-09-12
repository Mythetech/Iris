using Iris.Telemetry;

namespace Iris.Telemetry.Test;

public sealed class RecordingTelemetrySink : ITelemetrySink
{
    private readonly object _gate = new();
    private readonly List<ReceivedSpan> _spans = [];
    private readonly List<(OtlpReceiverStatus Status, string? Endpoint, string? Error)> _statuses = [];
    private TaskCompletionSource _firstBatch = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IReadOnlyList<ReceivedSpan> Spans { get { lock (_gate) return _spans.ToList(); } }
    public IReadOnlyList<(OtlpReceiverStatus Status, string? Endpoint, string? Error)> Statuses { get { lock (_gate) return _statuses.ToList(); } }
    public Task FirstBatch => _firstBatch.Task;

    public Task AcceptAsync(IReadOnlyList<ReceivedSpan> spans, CancellationToken cancellationToken)
    {
        lock (_gate) _spans.AddRange(spans);
        _firstBatch.TrySetResult();
        return Task.CompletedTask;
    }

    public Task StatusChangedAsync(OtlpReceiverStatus status, string? endpoint, string? error, CancellationToken cancellationToken)
    {
        lock (_gate) _statuses.Add((status, endpoint, error));
        return Task.CompletedTask;
    }
}
