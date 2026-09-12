using Iris.Components.Theme;
using Mythetech.Framework.Infrastructure.Settings;

namespace Iris.Components.Sagas;

public class SagaTelemetrySettings : SettingsBase
{
    public const int DefaultPort = 4318;

    public override string SettingsId => "SagaTelemetry";
    public override string DisplayName => "Saga Telemetry";
    public override string Icon => IrisIcons.Telemetry;
    public override int Order => 40;

    [Setting(
        Label = "Receive OpenTelemetry traces",
        Description = "Listen on localhost for OTLP/HTTP trace exports from your applications. The Sagas page uses them to show state machine transitions.")]
    public bool ReceiverEnabled { get; set; } = false;

    [Setting(
        Label = "Receiver port",
        Description = "Port the OTLP receiver binds on 127.0.0.1. Point your app's OTEL_EXPORTER_OTLP_ENDPOINT here.")]
    public int ReceiverPort { get; set; } = DefaultPort;

    public bool HasValidPort => ReceiverPort is >= 1024 and <= 65535;
}
