using System.Net.Sockets;
using Google.Protobuf;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace Iris.Telemetry;

/// <summary>
/// Loopback-only OTLP/HTTP receiver. One endpoint, POST /v1/traces, protobuf bodies only.
/// Everything it learns goes to the <see cref="ITelemetrySink"/>; it knows nothing about the UI.
/// </summary>
public sealed class OtlpReceiverHost : IOtlpReceiver, IAsyncDisposable, IDisposable
{
    private const string ProtobufContentType = "application/x-protobuf";
    private const string TracesPath = "/v1/traces";

    private readonly ITelemetrySink _sink;
    private readonly ILogger<OtlpReceiverHost> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WebApplication? _app;
    private bool _disposed;

    public OtlpReceiverHost(ITelemetrySink sink, ILogger<OtlpReceiverHost> logger)
    {
        _sink = sink;
        _logger = logger;
    }

    public OtlpReceiverStatus Status { get; private set; } = OtlpReceiverStatus.Stopped;
    public string? Endpoint { get; private set; }
    public int? Port { get; private set; }
    public string? LastError { get; private set; }

    public async Task StartAsync(int port, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_app is not null && Status == OtlpReceiverStatus.Listening && (Port == port || port == 0))
                return;

            if (_app is not null)
                await StopCoreAsync(cancellationToken);

            await SetStatusAsync(OtlpReceiverStatus.Starting, null, null, cancellationToken);

            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            // The default ConsoleLifetime installs POSIX signal handlers that cancel SIGINT, SIGQUIT and
            // SIGTERM so the signal stops this inner host instead of the process. Iris is the process, so
            // leaving it in place makes the whole app ignore Ctrl+C and logout for as long as the receiver
            // has ever run. Do not remove this registration.
            builder.Services.AddSingleton<IHostLifetime, NoopHostLifetime>();
            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
            var app = builder.Build();
            app.MapPost(TracesPath, HandleTracesAsync);

            try
            {
                await app.StartAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or SocketException)
            {
                _logger.LogWarning(ex, "OTLP receiver failed to bind port {Port}", port);
                await app.DisposeAsync();
                await SetStatusAsync(OtlpReceiverStatus.Failed, null, ex.Message, cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                await app.DisposeAsync();
                await SetStatusAsync(OtlpReceiverStatus.Failed, null, ex.Message, cancellationToken);
                throw;
            }

            var address = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault() ?? $"http://127.0.0.1:{port}";
            _app = app;
            Port = new Uri(address).Port;
            await SetStatusAsync(OtlpReceiverStatus.Listening, address, null, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await StopCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await StopAsync();
    }

    /// <summary>
    /// Synchronous teardown for containers that dispose their singletons on the sync path. It skips the
    /// graceful stop instead of blocking on it; disposing the inner host is enough to release the port.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var app = _app;
        _app = null;
        // WebApplication implements IDisposable explicitly, through IHost.
        ((IDisposable?)app)?.Dispose();
        Status = OtlpReceiverStatus.Stopped;
        Endpoint = null;
        Port = null;
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_app is not null)
            {
                var app = _app;
                _app = null;
                try
                {
                    await app.StopAsync(cancellationToken);
                }
                finally
                {
                    await app.DisposeAsync();
                }
            }
        }
        finally
        {
            Port = null;
            await SetStatusAsync(OtlpReceiverStatus.Stopped, null, null, cancellationToken);
        }
    }

    private async Task SetStatusAsync(OtlpReceiverStatus status, string? endpoint, string? error, CancellationToken cancellationToken)
    {
        Status = status;
        Endpoint = endpoint;
        LastError = error;
        try
        {
            await _sink.StatusChangedAsync(status, endpoint, error, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telemetry sink failed to process status change to {Status}", status);
        }
    }

    private async Task<IResult> HandleTracesAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.Headers.ContentEncoding.Count > 0)
            return Results.Text("Content-Encoding is not supported; export uncompressed", statusCode: StatusCodes.Status415UnsupportedMediaType);

        if (request.ContentType?.StartsWith(ProtobufContentType, StringComparison.OrdinalIgnoreCase) != true)
            return Results.Text($"Expected {ProtobufContentType}", statusCode: StatusCodes.Status415UnsupportedMediaType);

        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, cancellationToken);

        IReadOnlyList<ReceivedSpan> spans;
        try
        {
            spans = OtlpTraceDecoder.Decode(buffer.GetBuffer().AsMemory(0, (int)buffer.Length));
        }
        catch (InvalidProtocolBufferException ex)
        {
            _logger.LogWarning(ex, "Rejected undecodable OTLP trace payload of {Length} bytes", buffer.Length);
            return Results.Text("Body is not a valid ExportTraceServiceRequest", statusCode: StatusCodes.Status400BadRequest);
        }

        await _sink.AcceptAsync(spans, cancellationToken);
        return Results.Bytes(new ExportTraceServiceResponse().ToByteArray(), ProtobufContentType);
    }

    private sealed class NoopHostLifetime : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
