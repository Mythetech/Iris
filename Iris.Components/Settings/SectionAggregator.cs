namespace Iris.Components.Settings;

public interface ISectionAggregator
{
    public List<Section> GetSettingsSections();
}

public class SectionAggregator : ISectionAggregator
{
    private readonly IEnumerable<ISettingsSectionProvider> _sectionProviders;

    public SectionAggregator(IEnumerable<ISettingsSectionProvider> sectionProviders)
    {
        _sectionProviders = sectionProviders;
    }

    public List<Section> GetSettingsSections()
    {
        return _sectionProviders.Select(x => x.GetSection()).ToList();
    }  
}