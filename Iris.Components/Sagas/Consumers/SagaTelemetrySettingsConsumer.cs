using Iris.Telemetry.Messages;
using Mythetech.Framework.Infrastructure.MessageBus;
using Mythetech.Framework.Infrastructure.Settings.Events;

namespace Iris.Components.Sagas.Consumers;

/// <summary>
/// The settings dialog and the Sagas page both change the receiver through the setting; this is the
/// single place that turns the setting into receiver commands.
/// </summary>
public sealed class SagaTelemetrySettingsConsumer : IConsumer<SettingsModelChanged<SagaTelemetrySettings>>
{
    private readonly IMessageBus _bus;

    public SagaTelemetrySettingsConsumer(IMessageBus bus)
    {
        _bus = bus;
    }

    public Task Consume(SettingsModelChanged<SagaTelemetrySettings> message)
    {
        var settings = message.Settings;
        return settings.ReceiverEnabled && settings.HasValidPort
            ? _bus.PublishAsync(new StartOtlpReceiver(settings.ReceiverPort))
            : _bus.PublishAsync(new StopOtlpReceiver());
    }
}
