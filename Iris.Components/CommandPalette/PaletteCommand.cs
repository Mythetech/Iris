namespace Iris.Components.CommandPalette;

/// <summary>
/// A single command surfaced by the command palette.
/// </summary>
/// <param name="Id">Stable identifier (e.g. "nav.messaging"). Used for keys and aria-activedescendant.</param>
/// <param name="Title">Display title shown in the result row.</param>
/// <param name="Description">Optional secondary text shown beneath the title.</param>
/// <param name="Icon">MudBlazor icon string.</param>
/// <param name="Keywords">Extra search terms not visible in the title (e.g. "send", "compose" for Messaging).</param>
/// <param name="InvokeAsync">Action invoked when the user activates this command.</param>
/// <param name="Group">Optional group label used to render section headers in the result list.</param>
public sealed record PaletteCommand(
    string Id,
    string Title,
    string? Description,
    string Icon,
    IReadOnlyList<string> Keywords,
    Func<CancellationToken, Task> InvokeAsync,
    string? Group = null);
