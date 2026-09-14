using Iris.Brokers.Models;

namespace Iris.Brokers
{
    public interface IConnector
    {
        /// <summary>
        /// The broker family this connector speaks for. See <see cref="ConnectorProviders"/>.
        ///
        /// <para>
        /// Coarser than <see cref="IConnection.Name"/>: one connector can serve several
        /// transports, and <c>AzureConnector</c> reports <c>Azure</c> for both Azure Service
        /// Bus and Azure Queue Storage. It is the right identifier when the question is which
        /// credentials or SDK are in play, and the wrong one when the question is what goes on
        /// the wire.
        /// </para>
        /// </summary>
        public string Provider { get; }

        public Task<IConnection?> ConnectAsync(ConnectionData data, bool discoverEndpoints = true);

        public Task<IConnection?> ConnectAsync(ConnectionData data, CancellationToken cancellationToken, bool discoverEndpoints = true);
    }
}

