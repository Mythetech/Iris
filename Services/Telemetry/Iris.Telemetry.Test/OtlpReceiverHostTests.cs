using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Iris.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

namespace Iris.Telemetry.Test;

public class OtlpReceiverHostTests
{
    private const string ProtobufContentType = "application/x-protobuf";

    private static OtlpReceiverHost CreateHost(RecordingTelemetrySink sink)
        => new(sink, NullLogger<OtlpReceiverHost>.Instance);

    [Fact(DisplayName = "Starting on port 0 binds an ephemeral loopback port and reports Listening")]
    public async Task Start_Binds_Ephemeral_Port()
    {
        var sink = new RecordingTelemetrySink();
        await using var host = CreateHost(sink);

        await host.StartAsync(0);

        host.Status.Should().Be(OtlpReceiverStatus.Listening);
        host.Endpoint.Should().StartWith("http://127.0.0.1:");
        host.Endpoint.Should().NotEndWith(":0");
        sink.Statuses.Select(s => s.Status).Should().ContainInOrder(OtlpReceiverStatus.Starting, OtlpReceiverStatus.Listening);
    }

    [Fact(DisplayName = "Posting a protobuf batch hands decoded spans to the sink and returns 200")]
    public async Task Post_Delivers_Spans_To_Sink()
    {
        var sink = new RecordingTelemetrySink();
        await using var host = CreateHost(sink);
        await host.StartAsync(0);
        var body = OtlpTraceDecoderTests.BuildRequest("sample", "order-state process",
            new Dictionary<string, string> { ["messaging.masstransit.saga_id"] = "abc" });
        using var client = new HttpClient();
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(ProtobufContentType);

        var response = await client.PostAsync($"{host.Endpoint}/v1/traces", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProtobufContentType);
        sink.Spans.Should().ContainSingle(s => s.Name == "order-state process");
    }

    [Fact(DisplayName = "Non protobuf content types are rejected with 415")]
    public async Task Post_Rejects_Wrong_Content_Type()
    {
        var sink = new RecordingTelemetrySink();
        await using var host = CreateHost(sink);
        await host.StartAsync(0);
        using var client = new HttpClient();

        var response = await client.PostAsync($"{host.Endpoint}/v1/traces", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        sink.Spans.Should().BeEmpty();
    }

    [Fact(DisplayName = "Gzip encoded bodies are rejected with 415")]
    public async Task Post_Rejects_Gzip()
    {
        var sink = new RecordingTelemetrySink();
        await using var host = CreateHost(sink);
        await host.StartAsync(0);
        using var client = new HttpClient();
        var content = new ByteArrayContent([1, 2, 3]);
        content.Headers.ContentType = new MediaTypeHeaderValue(ProtobufContentType);
        content.Headers.ContentEncoding.Add("gzip");

        var response = await client.PostAsync($"{host.Endpoint}/v1/traces", content);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        sink.Spans.Should().BeEmpty();
    }

    [Fact(DisplayName = "Undecodable protobuf bodies are rejected with 400")]
    public async Task Post_Rejects_Garbage()
    {
        var sink = new RecordingTelemetrySink();
        await using var host = CreateHost(sink);
        await host.StartAsync(0);
        using var client = new HttpClient();
        var content = new ByteArrayContent([0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);
        content.Headers.ContentType = new MediaTypeHeaderValue(ProtobufContentType);

        var response = await client.PostAsync($"{host.Endpoint}/v1/traces", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        sink.Spans.Should().BeEmpty();
    }

    [Fact(DisplayName = "Starting on a busy port reports Failed with the socket error")]
    public async Task Start_On_Busy_Port_Reports_Failed()
    {
        var first = new RecordingTelemetrySink();
        await using var occupant = CreateHost(first);
        await occupant.StartAsync(0);
        var busyPort = occupant.Port!.Value;
        var sink = new RecordingTelemetrySink();
        await using var host = CreateHost(sink);

        await host.StartAsync(busyPort);

        host.Status.Should().Be(OtlpReceiverStatus.Failed);
        host.Endpoint.Should().BeNull();
        host.LastError.Should().NotBeNullOrWhiteSpace();
        sink.Statuses.Last().Status.Should().Be(OtlpReceiverStatus.Failed);
        sink.Statuses.Last().Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "Stop reports Stopped and the endpoint no longer accepts connections")]
    public async Task Stop_Reports_Stopped()
    {
        var sink = new RecordingTelemetrySink();
        await using var host = CreateHost(sink);
        await host.StartAsync(0);
        var endpoint = host.Endpoint!;

        await host.StopAsync();

        host.Status.Should().Be(OtlpReceiverStatus.Stopped);
        host.Endpoint.Should().BeNull();
        using var client = new HttpClient();
        var act = () => client.GetAsync(endpoint);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact(DisplayName = "Starting again on the same port while listening is a no-op")]
    public async Task Start_Same_Port_Is_Noop()
    {
        var sink = new RecordingTelemetrySink();
        await using var host = CreateHost(sink);
        await host.StartAsync(0);
        var port = host.Port!.Value;
        var statusCount = sink.Statuses.Count;

        await host.StartAsync(port);

        sink.Statuses.Count.Should().Be(statusCount);
        host.Port.Should().Be(port);
    }

    [Fact(DisplayName = "Disposing twice does not throw")]
    public async Task Dispose_Is_Idempotent()
    {
        var sink = new RecordingTelemetrySink();
        var host = CreateHost(sink);
        await host.StartAsync(0);
        await host.DisposeAsync();

        var act = async () => await host.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "A sink that throws on status changes does not break starting or disposing the host")]
    public async Task Start_And_Dispose_Survive_A_Throwing_Sink()
    {
        var host = new OtlpReceiverHost(new ThrowingStatusChangedSink(), NullLogger<OtlpReceiverHost>.Instance);

        var startAct = () => host.StartAsync(0);
        await startAct.Should().NotThrowAsync();
        host.Status.Should().Be(OtlpReceiverStatus.Listening);

        var disposeAct = async () => await host.DisposeAsync();
        await disposeAct.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "The real OpenTelemetry OTLP HTTP exporter round-trips a span with MassTransit saga tags")]
    public async Task Real_Exporter_Round_Trips()
    {
        var sink = new RecordingTelemetrySink();
        await using var host = CreateHost(sink);
        await host.StartAsync(0);
        const string sourceName = "Iris.Telemetry.Test";
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddOtlpExporter(options =>
            {
                options.Protocol = OtlpExportProtocol.HttpProtobuf;
                options.Endpoint = new Uri($"{host.Endpoint}/v1/traces");
                options.ExportProcessorType = ExportProcessorType.Simple;
            })
            .Build();
        using var source = new ActivitySource(sourceName);
        var sagaId = Guid.NewGuid().ToString("D");

        using (var activity = source.StartActivity("order-state process", ActivityKind.Consumer))
        {
            activity!.SetTag("messaging.masstransit.saga_id", sagaId);
            activity.SetTag("messaging.masstransit.begin_state", "Initial");
            activity.SetTag("messaging.masstransit.end_state", "Submitted");
        }
        tracerProvider!.ForceFlush(10_000).Should().BeTrue();
        await sink.FirstBatch.WaitAsync(TimeSpan.FromSeconds(10));

        var span = sink.Spans.Should().ContainSingle().Subject;
        span.Tags["messaging.masstransit.saga_id"].Should().Be(sagaId);
        span.Tags["messaging.masstransit.begin_state"].Should().Be("Initial");
        span.Tags["messaging.masstransit.end_state"].Should().Be("Submitted");
    }
}
