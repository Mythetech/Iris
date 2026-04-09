using System.Threading.Channels;
using Iris.Contracts.Brokers.Models;

namespace Iris.Components.NativeMenu;

public interface INativeMenuService
{
    bool IsActive { get; }

    void Initialize(object menuBar);

    ChannelReader<string> MenuItemClicks { get; }

    void SetItemEnabled(string itemId, bool enabled);

    void RebuildConnectionsMenu(IEnumerable<Provider> connections);
}
