using Iris.Components.Brokers;
using Iris.Components.Help;
using Iris.Components.NativeMenu;
using Iris.Components.NativeMenu.Commands;
using Iris.Components.Settings;
using Microsoft.Extensions.Logging;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Iris.Desktop.NativeMenu;

public class NativeMenuCommandDispatcher : INativeMenuCommandDispatcher
{
    private readonly IMessageBus _messageBus;
    private readonly ILogger<NativeMenuCommandDispatcher> _logger;

    private readonly Dictionary<string, Func<Task>> _handlers;

    public NativeMenuCommandDispatcher(
        IMessageBus messageBus,
        ILogger<NativeMenuCommandDispatcher> logger)
    {
        _messageBus = messageBus;
        _logger = logger;

        _handlers = new Dictionary<string, Func<Task>>
        {
            // App menu
            [MenuItemIds.IrisSettings] = () => _messageBus.PublishAsync(
                new ShowDialog(typeof(SettingsDialog), "Settings",
                    new DialogOptions { CloseOnEscapeKey = true, BackgroundClass = "iris-dialog", MaxWidth = MaxWidth.Large })),

            // File menu
            [MenuItemIds.FileNewMessage] = () => _messageBus.PublishAsync(new NavigateTo("/Messaging")),
            [MenuItemIds.FileSettings] = () => _messageBus.PublishAsync(
                new ShowDialog(typeof(SettingsDialog), "Settings",
                    new DialogOptions { CloseOnEscapeKey = true, BackgroundClass = "iris-dialog", MaxWidth = MaxWidth.Large })),

            // Connections
            [MenuItemIds.ConnectionsNew] = () => _messageBus.PublishAsync(
                new ShowDialog(typeof(AddConnectionDialog), "Add Connection",
                    new DialogOptions { MaxWidth = MaxWidth.Medium })),

            // History
            [MenuItemIds.HistoryView] = () => _messageBus.PublishAsync(new NavigateTo("/History")),
            [MenuItemIds.HistoryExport] = () => _messageBus.PublishAsync(new ExportHistoryAsJson()),
            [MenuItemIds.HistoryClear] = () => _messageBus.PublishAsync(new ClearHistory()),

            // Packages
            [MenuItemIds.PackagesView] = () => _messageBus.PublishAsync(new NavigateTo("/Packages")),
            [MenuItemIds.PackagesUpload] = () => _messageBus.PublishAsync(new TriggerPackageUpload()),

            // Help
            [MenuItemIds.HelpKeyboardShortcuts] = () => _messageBus.PublishAsync(
                new ShowDialog(typeof(KeyboardShortcutsDialog), "Keyboard Shortcuts",
                    new DialogOptions { CloseOnEscapeKey = true, BackgroundClass = "iris-dialog", MaxWidth = MaxWidth.Small })),
        };
    }

    public async Task HandleMenuItemClickAsync(string itemId)
    {
        try
        {
            if (_handlers.TryGetValue(itemId, out var handler))
            {
                await handler();
                return;
            }

            // Dynamic: connection actions
            if (itemId.StartsWith(MenuItemIds.ConnectionsPrefix))
            {
                var suffix = itemId[MenuItemIds.ConnectionsPrefix.Length..];

                if (suffix.EndsWith(MenuItemIds.ConnectionsSendMessageSuffix))
                {
                    var address = suffix[..^MenuItemIds.ConnectionsSendMessageSuffix.Length];
                    var providerAddress = UnsanitizeAddress(address);
                    await _messageBus.PublishAsync(new NavigateTo($"/Messaging?Provider={Uri.EscapeDataString(providerAddress)}"));
                    return;
                }

                if (suffix.EndsWith(MenuItemIds.ConnectionsInfoSuffix))
                {
                    var idToken = suffix[..^MenuItemIds.ConnectionsInfoSuffix.Length];
                    if (Guid.TryParseExact(idToken, "N", out var providerId))
                    {
                        await _messageBus.PublishAsync(new NavigateTo($"/connections/{providerId}"));
                    }
                    else
                    {
                        _logger.LogWarning("Could not parse provider id token from {ItemId}", itemId);
                    }
                    return;
                }
            }

            _logger.LogWarning("Unhandled menu item click: {ItemId}", itemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle menu item click: {ItemId}", itemId);
        }
    }

    private static string UnsanitizeAddress(string sanitized)
    {
        // Best-effort reverse of SanitizeId — works for common broker URLs
        // e.g. "amqp.localhost.5672" → "amqp://localhost:5672"
        var parts = sanitized.Split('.');
        if (parts.Length >= 3)
        {
            var protocol = parts[0];
            var rest = string.Join(".", parts[1..]);
            // Try to reconstruct as protocol://host:port
            var lastDot = rest.LastIndexOf('.');
            if (lastDot > 0 && int.TryParse(rest[(lastDot + 1)..], out _))
            {
                return $"{protocol}://{rest[..lastDot]}:{rest[(lastDot + 1)..]}";
            }

            return $"{protocol}://{rest}";
        }

        return sanitized;
    }
}
