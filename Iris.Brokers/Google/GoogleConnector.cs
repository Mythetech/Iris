using Grpc.Core;
using Iris.Brokers.Exceptions;
using Iris.Brokers.Models;
using ConnectorProviders = Iris.Contracts.Brokers.Models.ConnectorProviders;

namespace Iris.Brokers.Google;

public class GoogleConnector : IConnector
{
    public string Provider => ConnectorProviders.Google;

    public Task<IConnection?> ConnectAsync(ConnectionData data, bool discoverEndpoints = true)
        => ConnectAsync(data, CancellationToken.None, discoverEndpoints);

    public async Task<IConnection?> ConnectAsync(ConnectionData data, CancellationToken cancellationToken, bool discoverEndpoints = true)
    {
        try
        {
            var settings = await GoogleClientFactory.ResolveAsync(data, cancellationToken);
            var (publisher, subscriber) = await GoogleClientFactory.CreateClientsAsync(settings, cancellationToken);

            var connection = new PubSubConnection(
                new ConnectionMetadata { Connector = this, Address = settings.Address }, settings, publisher, subscriber);

            // Building the clients spoke to nothing. A real call is what proves the
            // credentials, the project and the host, so one is made either way.
            if (discoverEndpoints)
                await connection.DiscoverAsync(cancellationToken);
            else
                await connection.ProbeAsync(cancellationToken);

            return connection;
        }
        catch (InvalidOperationException ex) when (ex.InnerException is RpcException)
        {
            // Already worded for the user by the connection.
            throw new InvalidConnectionException(ex.Message, ex.InnerException);
        }
        catch (Exception ex) when (ex is not (InvalidConnectionException or OperationCanceledException))
        {
            // InvalidConnectionException is the only failure the connection dialog handles.
            // Anything else would leave it waiting, so nothing else is allowed out.
            throw new InvalidConnectionException(PubSubErrors.ConnectFailed, ex);
        }
    }
}
