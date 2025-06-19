using Iris.Components.Infrastructure;
using Microsoft.AspNetCore.Components;

namespace Iris.Components.Settings;

public abstract class Section : ComponentBase, IDynamicComponent
{
    public abstract string Name { get; init; }

    public int Order { get; set; } = 0;

    public List<Section>? SubSections { get; private set; } = default!;
    
    public abstract Type ComponentType { get; set; }

    public Dictionary<string, object> Parameters { get; set; } = new();

    protected void AddSubSection(Section section)
    {
        SubSections ??= [];
        
        SubSections.Add(section);
        
        SubSections.OrderBy(s => s.Order).ToList().ForEach(s => s.Order = ++section.Order);
    }
}