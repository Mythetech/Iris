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
            Verified = IsVerified(framework, connection),
        };
    }

    /// <summary>
    /// Matches the adapter's claim against the connection's transport first and its provider
    /// second, the same two-step lookup <c>ConnectionDetailsPage.ResolveView</c> uses, and for
    /// the same reason: neither identifier alone is right for every broker.
    ///
    /// <para>
    /// Matching on the provider alone, which is what this did, cannot distinguish Azure
    /// Service Bus from Azure Queue Storage, because one <c>AzureConnector</c> reports
    /// <c>Azure</c> for both. Those two speak different wire formats, so an adapter proven
    /// against one says nothing about the other, and a single claim covered both.
    /// </para>
    ///
    /// <para>
    /// Matching on the transport alone would break RabbitMQ, whose connection renames itself
    /// to <c>Docker</c> or <c>CloudAmpq</c> by address. That is a deployment label rather than
    /// a transport, and RabbitMQ has only the one transport anyway, so the provider is the
    /// stable identifier there. An adapter therefore claims at whichever granularity actually
    /// pins the wire format, and this accepts either.
    /// </para>
    /// </summary>
    private static bool IsVerified(IFramework framework, IConnection connection)
        => framework.VerifiedProviders.Contains(connection.Name)
           || framework.VerifiedProviders.Contains(connection.Connector.Provider);
}
