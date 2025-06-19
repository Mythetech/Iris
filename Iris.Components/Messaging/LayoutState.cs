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
    
    public event Action LayoutStateChanged;
    
    private void NotifyLayoutStateChanged() => LayoutStateChanged?.Invoke();

    private List<DynamicTabView>? _layout;

    public List<DynamicTabView>? Layout => _layout ?? GetDefaultTabLayout();

    public void UpdateTab(DynamicTabView dynamicTabView)
    {
        var tab = _layout?.FirstOrDefault(x => x.Id.Equals(dynamicTabView.Id));

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
        
        if (_layout == null || _defaultTabLayout.Count > _layout.Count)
        {
            _layout = _defaultTabLayout;
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

    private List<DynamicTabView> _defaultTabLayout => GetDefaultTabLayout();
    
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