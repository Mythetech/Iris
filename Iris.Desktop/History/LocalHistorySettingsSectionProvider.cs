using Iris.Components.Settings;

namespace Iris.Desktop.History;

public class LocalHistorySettingsSectionProvider : ISettingsSectionProvider
{
    public Section GetSection()
    {
        return new LocalHistorySection();
    }
}