using FluentAssertions;

namespace Iris.Telemetry.Test;

/// <summary>
/// Decodes a committed body that a real OpenTelemetry OTLP/HTTP exporter actually put on the
/// wire, captured from version 1.18.0 of the SDK.
///
/// <para>
/// <c>OtlpTraceDecoderTests</c> builds its inputs from the same generated protobuf types the
/// decoder reads them back with, so it cannot tell whether Iris's vendored <c>.proto</c> files
/// still match what an exporter emits: a change to both sides at once would pass. The
/// exporter round trip in <c>OtlpReceiverHostTests</c> closes that, but only against whichever
/// SDK version Iris happens to reference today.
/// </para>
///
/// <para>
/// Iris is the receiver, and the exporter belongs to whoever's application is being traced, so
/// it is their SDK version that has to keep working, not ours. These bytes stay pinned at the
/// version they were taken from for exactly that reason: they go on proving Iris can read a
/// 1.18.0 exporter long after the reference here has moved. Adding a capture from a newer SDK
/// means adding a file, not replacing this one.
/// </para>
///
/// <para>
/// Regenerate or add one with <c>CaptureOtlpExport</c>, which posts a real export to a
/// loopback listener and writes the body out verbatim.
/// </para>
/// </summary>
public class CapturedOtlpExportTests
{
    private static byte[] Capture(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact(DisplayName = "A body from a real 1.18.0 OTLP exporter decodes to the span that produced it")]
    public void Captured_export_decodes()
    {
        var spans = OtlpTraceDecoder.Decode(Capture("otlp-export-1.18.0.bin"));

        var span = spans.Should().ContainSingle().Subject;
        span.Name.Should().Be("order-state process");
        span.ServiceName.Should().NotBeNullOrWhiteSpace("the exporter always stamps a resource service.name");

        // The three tags Iris's saga view reads. A decoder that dropped attributes, or a
        // proto change that renamed the field they arrive in, leaves these empty rather than
        // throwing, and the diagram just stops moving.
        span.Tags["messaging.masstransit.saga_id"].Should().Be("5f0d6a1e-1c9d-4a5c-9f16-9d4a0a6e0b21");
        span.Tags["messaging.masstransit.begin_state"].Should().Be("Initial");
        span.Tags["messaging.masstransit.end_state"].Should().Be("Submitted");

        span.TraceId.Should().HaveLength(32);
        span.SpanId.Should().HaveLength(16);
        span.EndTime.Should().BeOnOrAfter(span.StartTime);
    }
}
