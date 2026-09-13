using System;
using Iris.Brokers.Models;

namespace Iris.Brokers
{
    public interface IConnection
    {
        public IConnector Connector { get; set; }

        public Guid Id { get; }

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
