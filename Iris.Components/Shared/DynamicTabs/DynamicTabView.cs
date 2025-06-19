using Iris.Components;
using Iris.Components.Infrastructure;
using Iris.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

public abstract class DynamicTabView : IrisBaseComponent, IDynamicComponent
{
    public virtual Guid Id { get; set; } = Guid.NewGuid();
    
    public abstract string Name { get; set; }

    public virtual string AreaIdentifier { get; set; } = "";
    
    public int AreaIndex { get; set; } = 0;

    public int? BadgeCount { get; set; }

    public bool IsActive { get; set; } = false;
    
    public abstract Type ComponentType { get; set; }
    public virtual Dictionary<string, object> Parameters { get; set; } = new();
}
