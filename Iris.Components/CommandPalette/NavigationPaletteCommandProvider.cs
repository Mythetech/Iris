using Iris.Components.Settings;
using Iris.Components.Theme;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Iris.Components.CommandPalette;

/// <summary>
/// Step-1 command provider. Surfaces the six full pages plus the Settings dialog
/// so users can jump to any of them via Cmd/Ctrl-K.
///
/// Named with the <c>Palette</c> infix to avoid colliding with
/// <see cref="Iris.Components.NativeMenu.Consumers.NavigationCommandProvider"/>,
/// which is an unrelated message-bus consumer for the native menu bar.
/// </summary>
public sealed class NavigationPaletteCommandProvider : ICommandProvider
{
    private const string NavigationGroup = "Navigation";
    private const string SettingsGroup = "Settings";

    private readonly NavigationManager _navigationManager;
    private readonly IDialogService _dialogService;

    public NavigationPaletteCommandProvider(
        NavigationManager navigationManager,
        IDialogService dialogService)
    {
        _navigationManager = navigationManager;
        _dialogService = dialogService;
    }

    public ValueTask<IReadOnlyList<PaletteCommand>> GetCommandsAsync(CancellationToken ct)
    {
        var commands = new PaletteCommand[]
        {
            Nav("nav.home", "Home", "Iris dashboard", IrisIcons.Home, "/", keywords: new[] { "dashboard", "start" }),
            Nav("nav.messaging", "Messaging", "Compose and send messages", IrisIcons.Send, "/Messaging",
                keywords: new[] { "send", "compose", "publish" }),
            Nav("nav.endpoints", "Endpoints", "Browse queues, topics, and subscriptions", IrisIcons.Endpoints, "/Endpoints",
                keywords: new[] { "queues", "topics", "subscriptions" }),
            Nav("nav.packages", "Packages", "Loaded assemblies and types", IrisIcons.Packages, "/Packages",
                keywords: new[] { "assemblies", "dlls", "types" }),
            Nav("nav.connections", "Connections", "Configured broker connections", IrisIcons.Connections, "/Connections",
                keywords: new[] { "brokers" }),
            Nav("nav.history", "History", "Recent activity and sent messages", IrisIcons.History, "/History",
                keywords: new[] { "log", "past", "recent" }),
            new PaletteCommand(
                Id: "settings.open",
                Title: "Open Settings…",
                Description: "Edit Iris settings",
                Icon: IrisIcons.Settings,
                Keywords: new[] { "preferences", "options", "configure" },
                InvokeAsync: OpenSettingsAsync,
                Group: SettingsGroup),
        };

        return ValueTask.FromResult<IReadOnlyList<PaletteCommand>>(commands);
    }

    private PaletteCommand Nav(
        string id,
        string title,
        string description,
        string icon,
        string path,
        IReadOnlyList<string>? keywords = null) =>
        new(
            Id: id,
            Title: title,
            Description: description,
            Icon: icon,
            Keywords: keywords ?? Array.Empty<string>(),
            InvokeAsync: _ => NavigateAsync(path),
            Group: NavigationGroup);

    private Task NavigateAsync(string path)
    {
        _navigationManager.NavigateTo(path);
        return Task.CompletedTask;
    }

    private async Task OpenSettingsAsync(CancellationToken ct)
    {
        await _dialogService.ShowAsync(
            typeof(SettingsDialog),
            "Settings",
            new DialogOptions
            {
                CloseOnEscapeKey = true,
                BackgroundClass = "iris-dialog",
                MaxWidth = MaxWidth.Large,
            });
    }
}
