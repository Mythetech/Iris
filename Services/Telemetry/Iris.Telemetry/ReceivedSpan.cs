namespace Iris.Telemetry;

public sealed record ReceivedSpan(
    string TraceId,
    string SpanId,
    string? ParentSpanId,
    string Name,
    string ServiceName,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    IReadOnlyDictionary<string, string> Tags);
