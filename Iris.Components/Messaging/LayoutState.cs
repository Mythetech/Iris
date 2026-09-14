namespace Iris.Components.Messaging;

public interface IMessagingLayoutService
{ 
    Task SaveLayoutAsync(List<DynamicTabView> layout);

    Task<List<DynamicTabView>?> LoadLayoutAsync();
}

public static class LayoutZones
{
    public const string TopZone = "top-dropzone";
    
    public const string BottomZone = "bottom-dropzone";
}

public class LayoutState
{
    private readonly IMessagingLayoutService _layoutService;

    public LayoutState(IMessagingLayoutService layoutService)
    {
        _layoutService = layoutService;
    }
    
    public event Action? LayoutStateChanged;
    
    private void NotifyLayoutStateChanged() => LayoutStateChanged?.Invoke();

    private List<DynamicTabView>? _layout;

    private List<DynamicTabView>? _defaultLayout;

    /// <summary>
    /// The default layout, allocated once. <c>OptionsPanel</c> hands whatever this returns
    /// to <c>TabDropContainer</c>, which moves the tabs in place, and <see cref="UpdateTab"/>
    /// then finds them by id. A fresh list of freshly minted tabs on each read would make
    /// every one of those steps operate on an instance nobody else holds.
    /// </summary>
    private List<DynamicTabView> DefaultLayout => _defaultLayout ??= GetDefaultTabLayout();

    /// <summary>
    /// The layout in force: the saved one once it has loaded, the default until then. Never
    /// null, and stable across reads, so the caller rendering it and the caller mutating it
    /// are looking at the same tabs.
    /// </summary>
    public List<DynamicTabView> Layout => _layout ?? DefaultLayout;

    public void UpdateTab(DynamicTabView dynamicTabView)
    {
        // Layout rather than the backing field, which is null until a load completes. A drop
        // in that window was accepted by the container and then silently discarded.
        var tab = Layout.FirstOrDefault(x => x.Id.Equals(dynamicTabView.Id));

        if (tab == null) 
            return;
        
        tab.AreaIdentifier = dynamicTabView.AreaIdentifier;
        tab.AreaIndex = dynamicTabView.AreaIndex;
        tab.BadgeCount = dynamicTabView.BadgeCount;
        
        NotifyLayoutStateChanged();
    }

    public async Task<List<DynamicTabView>?> GetLayoutAsync()
    {
        if(_layout?.Count > 0 )
            return _layout;
        
        _layout = await _layoutService.LoadLayoutAsync();

        // A release that adds a tab leaves every existing user with a layout one short, and
        // the panel would render without the new tab and no way to reach it. The cost is
        // that such a release also discards the user's arrangement.
        if (_layout == null || DefaultLayout.Count > _layout.Count)
        {
            _layout = DefaultLayout;
        }

        await Task.Yield();
        
        NotifyLayoutStateChanged();
        
        return _layout;
    }

    public async Task SaveLayoutAsync(List<DynamicTabView> layout)
    {
        await _layoutService.SaveLayoutAsync(layout);
        
        _layout = layout;
        
        NotifyLayoutStateChanged();
    }

    /// <summary>
    /// A new default layout every time, which is what resetting from the settings panel
    /// needs: the tabs it replaces are mutated in place, so reinstating the live instance
    /// would reset nothing. Callers wanting the layout in force want <see cref="Layout"/>.
    /// </summary>
    public List<DynamicTabView> GetDefaultTabLayout()
    {
        return
        [
            new MessageOptions()
            {
                AreaIdentifier = LayoutZones.TopZone,
                AreaIndex = 0,
                IsActive = true,
            },

            new TemplateTabList()
            {
                AreaIdentifier = LayoutZones.TopZone,
                AreaIndex = 1,
            },

            new FrameworkSelector()
            {
                AreaIdentifier = LayoutZones.TopZone,
                AreaIndex = 2,
            },

            new MessageHeaders()
            {
                AreaIdentifier = LayoutZones.BottomZone,
                IsActive = true,
                AreaIndex = 0,
            },
            
            new RecentHistoryTabList()
            {
                AreaIdentifier = LayoutZones.BottomZone,
                AreaIndex = 1,
            }
        ];
    }
}