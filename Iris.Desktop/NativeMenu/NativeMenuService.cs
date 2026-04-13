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
    private readonly List<string> _menuOrder = new();

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
        if (_menuBar is null) return;

        if (_menuBar.ContainsMenu("Connections"))
            _menuBar.RemoveMenu("Connections");

        var insertIndex = GetMenuInsertIndex("Connections");
        _menuBar.AddMenu("Connections", insertIndex, menu =>
        {
            menu.AddItem("New Connection...", MenuItemIds.ConnectionsNew);
            menu.AddSeparator();

            foreach (var provider in connections)
            {
                var sanitizedAddress = SanitizeId(provider.Address);
                var idToken = provider.Id.ToString("N");
                menu.AddSubmenu(provider.Name ?? provider.Address, sub =>
                {
                    sub.AddItem("Send Message",
                        MenuItemIds.ConnectionsPrefix + sanitizedAddress + MenuItemIds.ConnectionsSendMessageSuffix);
                    sub.AddItem("Connection Info",
                        MenuItemIds.ConnectionsPrefix + idToken + MenuItemIds.ConnectionsInfoSuffix);
                });
            }
        });
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
        _menuOrder.Add("File");

        // Connections menu
        _menuBar.AddMenu("Connections", menu =>
        {
            menu.AddItem("New Connection...", MenuItemIds.ConnectionsNew);
            menu.AddSeparator();
        });
        _menuOrder.Add("Connections");

        // History menu
        _menuBar.AddMenu("History", menu =>
        {
            menu.AddItem("View History", MenuItemIds.HistoryView);
            menu.AddSeparator();
            menu.AddItem("Export History", MenuItemIds.HistoryExport);
            menu.AddItem("Clear History", MenuItemIds.HistoryClear);
        });
        _menuOrder.Add("History");

        // Packages menu
        _menuBar.AddMenu("Packages", menu =>
        {
            menu.AddItem("View Packages", MenuItemIds.PackagesView);
            menu.AddSeparator();
            menu.AddItem("Upload Package...", MenuItemIds.PackagesUpload);
        });
        _menuOrder.Add("Packages");

        // Help menu
        _menuBar.AddMenu("Help", menu =>
        {
            menu.AddItem("Keyboard Shortcuts", MenuItemIds.HelpKeyboardShortcuts, item =>
                item.WithAccelerator(OperatingSystem.IsMacOS() ? "Cmd+?" : "Ctrl+?"));
        });
        _menuOrder.Add("Help");
    }

    private int GetMenuInsertIndex(string label)
    {
        var index = _menuOrder.IndexOf(label);
        if (index < 0) return -1;

        var position = 1; // app menu is at native index 0
        for (var i = 0; i < index; i++)
        {
            if (_menuBar!.ContainsMenu(_menuOrder[i]))
                position++;
        }
        return position;
    }

    private static string SanitizeId(string input)
        => input.Replace("://", ".").Replace("/", ".").Replace(":", ".").TrimEnd('.');
}
