using Iris.Components.Sagas;
using Iris.Telemetry;
using Mythetech.Framework.Infrastructure.Initialization;

namespace Iris.Desktop.Infrastructure;

/// <summary>Starts the OTLP receiver at launch when the persisted setting says so. Runs after settings load (order 100).</summary>
public class SagaTelemetryInitializationHook : IAsyncInitializationHook
{
    private readonly SagaTelemetrySettings _settings;
    private readonly IOtlpReceiver _receiver;

    public SagaTelemetryInitializationHook(SagaTelemetrySettings settings, IOtlpReceiver receiver)
    {
        _settings = settings;
        _receiver = receiver;
    }

    public int Order => 800;
    public string Name => "Saga Telemetry Receiver";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_settings.ReceiverEnabled && _settings.HasValidPort)
            await _receiver.StartAsync(_settings.ReceiverPort, cancellationToken);
    }
}
