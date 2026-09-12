using FluentAssertions;
using Google.Protobuf;
using Iris.Telemetry;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;

namespace Iris.Telemetry.Test;

public class OtlpTraceDecoderTests
{
    private static readonly byte[] TraceIdBytes = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
    private static readonly byte[] SpanIdBytes = Enumerable.Range(1, 8).Select(i => (byte)(i * 16)).ToArray();

    public static byte[] BuildRequest(string serviceName, string spanName, IReadOnlyDictionary<string, string> tags)
    {
        var resource = new Resource();
        resource.Attributes.Add(new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = serviceName } });

        var span = new Span
        {
            TraceId = ByteString.CopyFrom(TraceIdBytes),
            SpanId = ByteString.CopyFrom(SpanIdBytes),
            Name = spanName,
            StartTimeUnixNano = 1_700_000_000_000_000_000UL,
            EndTimeUnixNano = 1_700_000_000_500_000_000UL,
        };
        foreach (var (key, value) in tags)
            span.Attributes.Add(new KeyValue { Key = key, Value = new AnyValue { StringValue = value } });
        span.Attributes.Add(new KeyValue { Key = "count", Value = new AnyValue { IntValue = 42 } });
        span.Attributes.Add(new KeyValue { Key = "flag", Value = new AnyValue { BoolValue = true } });

        var scopeSpans = new ScopeSpans();
        scopeSpans.Spans.Add(span);
        var resourceSpans = new ResourceSpans { Resource = resource };
        resourceSpans.ScopeSpans.Add(scopeSpans);
        var request = new ExportTraceServiceRequest();
        request.ResourceSpans.Add(resourceSpans);
        return request.ToByteArray();
    }

    [Fact(DisplayName = "Decodes resource service name, ids, name, times and string tags")]
    public void Decodes_Span_Fields()
    {
        // Arrange
        var body = BuildRequest("sample", "order-state process",
            new Dictionary<string, string> { ["messaging.masstransit.saga_id"] = "abc" });

        // Act
        var spans = OtlpTraceDecoder.Decode(body);

        // Assert
        spans.Should().HaveCount(1);
        var span = spans[0];
        span.ServiceName.Should().Be("sample");
        span.Name.Should().Be("order-state process");
        span.TraceId.Should().Be("0102030405060708090a0b0c0d0e0f10");
        span.SpanId.Should().Be("1020304050607080");
        span.ParentSpanId.Should().BeNull();
        span.StartTime.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));
        span.EndTime.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000).AddMilliseconds(500));
        span.Tags["messaging.masstransit.saga_id"].Should().Be("abc");
    }

    [Fact(DisplayName = "Flattens non-string attribute values to invariant strings")]
    public void Flattens_NonString_Attributes()
    {
        var spans = OtlpTraceDecoder.Decode(BuildRequest("s", "n", new Dictionary<string, string>()));

        spans[0].Tags["count"].Should().Be("42");
        spans[0].Tags["flag"].Should().Be("true");
    }

    [Fact(DisplayName = "Throws InvalidProtocolBufferException for garbage bodies")]
    public void Throws_On_Garbage()
    {
        var act = () => OtlpTraceDecoder.Decode(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });

        act.Should().Throw<InvalidProtocolBufferException>();
    }
}
