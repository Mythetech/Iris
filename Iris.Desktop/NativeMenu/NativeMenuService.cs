using System.Threading.Channels;
using Hermes.Menu;
using Iris.Components.NativeMenu;
using Iris.Contracts.Brokers.Models;
using Microsoft.Extensions.Logging;

namespace Iris.Desktop.NativeMenu;

public class NativeMenuService : INativeMenuService
{
    private readonly ILogger<NativeMenuService> _logger;
    private readonly Channel<string> _clickChannel;

    private NativeMenuBar? _menuBar;
    private bool _isInitialized;

    private Hermes.Menu.NativeMenu? _connectionsMenu;

    public NativeMenuService(ILogger<NativeMenuService> logger)
    {
        _logger = logger;
        _clickChannel = Channel.CreateUnbounded<string>();
    }

    public bool IsActive => _isInitialized;

    public ChannelReader<string> MenuItemClicks => _clickChannel.Reader;

    public void Initialize(object menuBar)
    {
        if (_isInitialized)
            return;

        try
        {
            _menuBar = (NativeMenuBar)menuBar;
            BuildMenuStructure();
            _menuBar.ItemClicked += OnNativeMenuItemClicked;
            _isInitialized = true;
            _logger.LogInformation("Native menus initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize native menus");
        }
    }

    public void SetItemEnabled(string itemId, bool enabled)
    {
        if (_menuBar?.TryGetItem(itemId, out var item) == true && item is not null)
        {
            item.IsEnabled = enabled;
        }
    }

    public void RebuildConnectionsMenu(IEnumerable<Provider> connections)
    {
        if (_connectionsMenu is null) return;

        // Remove all existing dynamic connection items
        var existingIds = _connectionsMenu.Items
            .Where(i => i.Id.StartsWith(MenuItemIds.ConnectionsPrefix))
            .Select(i => i.Id)
            .ToList();

        foreach (var id in existingIds)
        {
            _connectionsMenu.RemoveItem(id);
        }

        var providers = connections.ToList();
        if (providers.Count > 0)
        {
            // Add separator before dynamic items if not already present
            foreach (var provider in providers)
            {
                var sanitizedAddress = SanitizeId(provider.Address);
                _connectionsMenu.AddSubmenu(provider.Name ?? provider.Address, sub =>
                {
                    sub.AddItem("Send Message",
                        MenuItemIds.ConnectionsPrefix + sanitizedAddress + MenuItemIds.ConnectionsSendMessageSuffix);
                    sub.AddItem("Connection Info",
                        MenuItemIds.ConnectionsPrefix + sanitizedAddress + MenuItemIds.ConnectionsInfoSuffix,
                        item => item.WithEnabled(false));
                });
            }
        }
    }

    private void OnNativeMenuItemClicked(string itemId)
    {
        _logger.LogDebug("Menu item clicked: {ItemId}", itemId);
        _clickChannel.Writer.TryWrite(itemId);
    }

    private void BuildMenuStructure()
    {
        if (_menuBar is null)
            return;

        // App menu
        _menuBar.AppMenu
            .AddItem("Settings...", MenuItemIds.IrisSettings, item =>
            {
                if (OperatingSystem.IsMacOS())
                    item.WithAccelerator("Cmd+,");
            });

        // File menu
        _menuBar.AddMenu("File", menu =>
        {
            menu.AddItem("New Message", MenuItemIds.FileNewMessage, item =>
                item.WithAccelerator("Ctrl+N"));
            menu.AddSeparator();
            menu.AddItem("Settings", MenuItemIds.FileSettings);
        });

        // Connections menu
        _menuBar.AddMenu("Connections", menu =>
        {
            _connectionsMenu = menu;
            menu.AddItem("New Connection...", MenuItemIds.ConnectionsNew);
            menu.AddSeparator();
            // Dynamic connection items will be added by RebuildConnectionsMenu
        });

        // History menu
        _menuBar.AddMenu("History", menu =>
        {
            menu.AddItem("View History", MenuItemIds.HistoryView);
            menu.AddSeparator();
            menu.AddItem("Export History", MenuItemIds.HistoryExport);
            menu.AddItem("Clear History", MenuItemIds.HistoryClear);
        });

        // Packages menu
        _menuBar.AddMenu("Packages", menu =>
        {
            menu.AddItem("View Packages", MenuItemIds.PackagesView);
            menu.AddSeparator();
            menu.AddItem("Upload Package...", MenuItemIds.PackagesUpload);
        });
    }

    private static string SanitizeId(string input)
        => input.Replace("://", ".").Replace("/", ".").Replace(":", ".").TrimEnd('.');
}
