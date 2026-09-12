namespace Iris.Telemetry.Messages;

public sealed record SpanBatchReceived(IReadOnlyList<ReceivedSpan> Spans);
