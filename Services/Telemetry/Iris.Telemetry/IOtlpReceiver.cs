namespace Iris.Telemetry;

public interface IOtlpReceiver
{
    OtlpReceiverStatus Status { get; }
    string? Endpoint { get; }
    int? Port { get; }
    string? LastError { get; }
    Task StartAsync(int port, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
