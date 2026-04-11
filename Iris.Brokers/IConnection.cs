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

        public Task SendAsync(EndpointDetails endpoint, string json);

        public Task SendAsync(EndpointDetails endpoint, MessageRequest message)
        {
            return SendAsync(endpoint, message.Json);
        }
    }
}

