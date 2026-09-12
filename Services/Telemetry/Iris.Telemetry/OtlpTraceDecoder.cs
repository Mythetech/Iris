using System.Globalization;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;

namespace Iris.Telemetry;

public static class OtlpTraceDecoder
{
    private const string ServiceNameAttribute = "service.name";

    public static IReadOnlyList<ReceivedSpan> Decode(ReadOnlyMemory<byte> body)
    {
        var request = ExportTraceServiceRequest.Parser.ParseFrom(body.Span);
        var spans = new List<ReceivedSpan>();

        foreach (var resourceSpans in request.ResourceSpans)
        {
            var serviceName = resourceSpans.Resource?.Attributes
                .FirstOrDefault(a => a.Key == ServiceNameAttribute)?.Value is { } value
                ? Flatten(value)
                : string.Empty;

            foreach (var scopeSpans in resourceSpans.ScopeSpans)
            {
                foreach (var span in scopeSpans.Spans)
                {
                    var tags = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var attribute in span.Attributes)
                        tags[attribute.Key] = Flatten(attribute.Value);

                    spans.Add(new ReceivedSpan(
                        TraceId: Convert.ToHexStringLower(span.TraceId.Span),
                        SpanId: Convert.ToHexStringLower(span.SpanId.Span),
                        ParentSpanId: span.ParentSpanId.IsEmpty ? null : Convert.ToHexStringLower(span.ParentSpanId.Span),
                        Name: span.Name,
                        ServiceName: serviceName,
                        StartTime: FromUnixNanos(span.StartTimeUnixNano),
                        EndTime: FromUnixNanos(span.EndTimeUnixNano),
                        Tags: tags));
                }
            }
        }

        return spans;
    }

    private static DateTimeOffset FromUnixNanos(ulong nanos)
        => DateTimeOffset.UnixEpoch.AddTicks((long)(nanos / 100));

    private static string Flatten(AnyValue value) => value.ValueCase switch
    {
        AnyValue.ValueOneofCase.StringValue => value.StringValue,
        AnyValue.ValueOneofCase.BoolValue => value.BoolValue ? "true" : "false",
        AnyValue.ValueOneofCase.IntValue => value.IntValue.ToString(CultureInfo.InvariantCulture),
        AnyValue.ValueOneofCase.DoubleValue => value.DoubleValue.ToString(CultureInfo.InvariantCulture),
        AnyValue.ValueOneofCase.ArrayValue => string.Join(",", value.ArrayValue.Values.Select(Flatten)),
        _ => value.ToString(),
    };
}
