using MudBlazor;
using Mythetech.Framework.Infrastructure.Settings;

namespace Iris.Desktop.History;

public class HistorySettings : SettingsBase
{
    public override string SettingsId => "History";
    public override string DisplayName => "Local History";
    public override string Icon => Icons.Material.Filled.History;
    public override int Order => 20;

    public override Type? EndingContent => typeof(HistoryManagementPanel);
}
