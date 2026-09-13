using Iris.Brokers;
using Iris.Brokers.Frameworks;
using Iris.Contracts.Messaging.Frameworks;

namespace Iris.Desktop.Brokers;

public sealed class LocalFrameworkCatalog : IFrameworkCatalog
{
    private readonly IEnumerable<IFramework> _frameworks;
    private readonly IBrokerConnectionManager _connections;

    public LocalFrameworkCatalog(IEnumerable<IFramework> frameworks, IBrokerConnectionManager connections)
    {
        _frameworks = frameworks;
        _connections = connections;
    }

    public async Task<IReadOnlyList<FrameworkDescriptor>> GetFrameworksAsync(string? providerAddress, int userHeaderCount)
    {
        var connection = string.IsNullOrWhiteSpace(providerAddress)
            ? null
            : await _connections.GetConnectionAsync(providerAddress);

        return _frameworks
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => Describe(f, connection, userHeaderCount))
            .ToList();
    }

    private static FrameworkDescriptor Describe(IFramework framework, IConnection? connection, int userHeaderCount)
    {
        if (connection is null)
            return framework.Descriptor;

        var result = FrameworkCompatibility.Check(framework, connection, userHeaderCount);

        return framework.Descriptor with
        {
            Supported = result.Supported,
            UnsupportedReason = result.Reason,
            DroppedKeys = result.DroppedKeys.Select(k => k.Name).ToList(),
            Verified = framework.VerifiedProviders.Contains(connection.Connector.Provider),
        };
    }
}
