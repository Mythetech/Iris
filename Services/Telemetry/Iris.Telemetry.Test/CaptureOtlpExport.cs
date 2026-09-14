using System.Diagnostics;
using System.Net;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

namespace Iris.Telemetry.Test;

/// <summary>
/// Produces the binary fixtures <see cref="CapturedOtlpExportTests"/> reads, by running a real
/// OTLP/HTTP exporter against a loopback listener and writing the request body out untouched.
///
/// <para>
/// Not a test. It is here rather than in a separate tool because it needs the same exporter
/// reference the test project already carries, and because a fixture whose provenance is a
/// script nobody can find is a fixture nobody trusts.
/// </para>
///
/// <para>
/// To add a capture: reference the SDK version you want to capture from, call
/// <see cref="WriteAsync"/> with a path ending in that version, and commit the result. The
/// span below is deliberately fixed, ids included, so a regenerated file differs only where
/// the wire format does.
/// </para>
/// </summary>
public static class CaptureOtlpExport
{
    /// <summary>Fixed so a recapture produces a body that differs only where the format does.</summary>
    public const string SagaId = "5f0d6a1e-1c9d-4a5c-9f16-9d4a0a6e0b21";

    public static async Task WriteAsync(string path)
    {
        var port = FreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        var captured = listener.GetContextAsync();

        Export($"http://127.0.0.1:{port}/v1/traces");

        var context = await captured;
        using var body = new MemoryStream();
        await context.Request.InputStream.CopyToAsync(body);

        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/x-protobuf";
        context.Response.Close();

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, body.ToArray());
    }

    private static void Export(string endpoint)
    {
        const string sourceName = "Iris.Telemetry.Capture";

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddOtlpExporter(options =>
            {
                options.Protocol = OtlpExportProtocol.HttpProtobuf;
                options.Endpoint = new Uri(endpoint);
                options.ExportProcessorType = ExportProcessorType.Simple;
            })
            .Build();

        using var source = new ActivitySource(sourceName);

        using (var activity = source.StartActivity("order-state process", ActivityKind.Consumer))
        {
            activity!.SetTag("messaging.masstransit.saga_id", SagaId);
            activity.SetTag("messaging.masstransit.begin_state", "Initial");
            activity.SetTag("messaging.masstransit.end_state", "Submitted");
        }

        provider.ForceFlush(10_000);
    }

    private static int FreePort()
    {
        using var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
