using Iris.Components.Theme;
using Mythetech.Framework.Infrastructure.Settings;

namespace Iris.Desktop.History;

public class HistorySettings : SettingsBase
{
    public override string SettingsId => "History";
    public override string DisplayName => "Local History";
    public override string Icon => IrisIcons.History;
    public override int Order => 20;

    [Setting(
        Label = "Records to keep",
        Description = "Oldest entries beyond this are removed as new ones are recorded. History shares the local database with connections, templates and packages, and it grew without bound. Set to 0 to keep everything.")]
    public int MaxRecords { get; set; } = 1000;

    public override Type? EndingContent => typeof(HistoryManagementPanel);
}
