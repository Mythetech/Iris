using MudBlazor;
using Mythetech.Framework.Infrastructure.Settings;

namespace Iris.Components.Brokers;

public class BrokerOperationSettings : SettingsBase
{
    public override string SettingsId => "BrokerOperations";
    public override string DisplayName => "Broker Operations";
    public override string Icon => Icons.Material.Filled.Warning;
    public override int Order => 20;

    [Setting(
        Label = "Require confirmation on destructive reads",
        Description = "Show a confirmation dialog before consuming messages off a queue. Consuming is irrevocable.")]
    public bool RequireConfirmationOnDestructiveRead { get; set; } = true;
}
