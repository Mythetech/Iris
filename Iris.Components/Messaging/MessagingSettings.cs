using MudBlazor;
using Mythetech.Framework.Infrastructure.Settings;

namespace Iris.Components.Messaging;

public class MessagingSettings : SettingsBase
{
    public override string SettingsId => "Messaging";
    public override string DisplayName => "Messaging";
    public override string Icon => Icons.Material.Filled.Email;
    public override int Order => 10;

    [Setting(Label = "Iris Key", Description = "Send requests with additional header to uniquely identify test requests")]
    public bool SendIrisHeader { get; set; }

    public override Type? EndingContent => typeof(LayoutSettingsDisplay);
}
