using System;
using Iris.Brokers.Models;

namespace Iris.Brokers
{
    public interface IConnection
    {
        public IConnector Connector { get; set; }

        public Guid Id { get; }

        /// <summary>
        /// The transport this connection speaks, surfaced in the UI as
        /// <c>Provider.Transport</c>. See <see cref="ConnectorTransports"/>.
        ///
        /// <para>
        /// Finer than <see cref="IConnector.Provider"/>, and the one to reason about when
        /// what matters is the wire format: <c>AzureServiceBus</c> and
        /// <c>AzureQueueStorage</c> are two transports behind a single <c>Azure</c> provider.
        /// </para>
        ///
        /// <para>
        /// Not guaranteed stable per broker. <c>RabbitMqConnection</c> overwrites this with a
        /// deployment label, <c>Docker</c> or <c>CloudAmpq</c>, depending on the address, so
        /// code that has to identify a broker checks this first and falls back to the
        /// provider. <c>LocalFrameworkCatalog.IsVerified</c> and
        /// <c>ConnectionDetailsPage.ResolveView</c> both do that.
        /// </para>
        /// </summary>
        public string Name { get; }

        public string Address { get; }

        public int EndpointCount { get; }

        public Task<List<EndpointDetails>> GetEndpointsAsync();

        /// <summary>
        /// Sends the request body. Only the body is guaranteed; a connection that also
        /// maps <see cref="MessageRequest.Headers"/> or <see cref="MessageRequest.TransportProperties"/>
        /// says so by implementing <see cref="IHeaderCarrier"/> / <see cref="ITransportPropertyCarrier"/>.
        /// </summary>
        public Task SendAsync(EndpointDetails endpoint, MessageRequest message);
    }
}
