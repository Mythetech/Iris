using MudBlazor;
using Mythetech.Framework.Infrastructure.Settings;

namespace Iris.Components.PackageManagement;

public class AssemblySettings : SettingsBase
{
    public override string SettingsId => "Assemblies";
    public override string DisplayName => "Assemblies";
    public override string Icon => Icons.Material.Filled.Extension;
    public override int Order => 20;

    [Setting(Label = "Max Type Depth", Description = "Maximum depth for recursive type mapping (default 3)")]
    public int MaxTypeDepth { get; set; } = 3;
}
