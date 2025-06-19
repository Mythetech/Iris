namespace Iris.Components.Settings;

public interface ISettingsSectionProvider
{
    public Section GetSection();

    public List<Section> GetSections()
    {
        return [GetSection()];
    }
}