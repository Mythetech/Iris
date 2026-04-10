using Iris.Desktop.Brokers;
using Mythetech.Framework.Infrastructure.Initialization;

namespace Iris.Desktop.Infrastructure;

public class AutoDiscoveryInitializationHook : IAsyncInitializationHook
{
    private readonly AutoDiscovery _autoDiscovery;

    public AutoDiscoveryInitializationHook(AutoDiscovery autoDiscovery)
    {
        _autoDiscovery = autoDiscovery;
    }

    public int Order => 600;

    public string Name => "Auto Discovery";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _autoDiscovery.DiscoverLocalConnectionsAsync();
    }
}
