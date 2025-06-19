using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Iris.Components.Shared.Time;

public sealed class LocalTime : ComponentBase, IDisposable
{
    [Inject]
    public TimeProvider TimeProvider { get; set; } = default!;

    [Parameter]
    public DateTime? DateTime { get; set; }
    
    [Parameter]
    public DateTimeOffset? DateTimeOffset { get; set; }

    protected override void OnInitialized()
    {
        if (TimeProvider is BrowserTimeProvider browserTimeProvider)
        {
            browserTimeProvider.LocalTimeZoneChanged += LocalTimeZoneChanged;
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (DateTimeOffset.HasValue && DateTime.HasValue)
        {
            throw new Exception("Must specify one of DateTime or DateTimeOffset");
        }
        
        if (DateTime != null)
        {
            builder.AddContent(0, TimeProvider.ToLocalDateTime(DateTime.Value));
        }
        
        if (DateTimeOffset != null)
        {
            builder.AddContent(0, TimeProvider.ToLocalDateTime(DateTimeOffset.Value));
        }
    }

    public void Dispose()
    {
        if (TimeProvider is BrowserTimeProvider browserTimeProvider)
        {
            browserTimeProvider.LocalTimeZoneChanged -= LocalTimeZoneChanged;
        }
    }

    private void LocalTimeZoneChanged(object? sender, EventArgs e)
    {
        _ = InvokeAsync(StateHasChanged);
    }
}