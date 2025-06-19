using System;
using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Iris.Brokers.Models;

namespace Iris.Brokers.Azure
{
    public class AzureServiceBusConnection : IConnection
    {
        private readonly ConnectionMetadata _metadata;
        private readonly ServiceBusAdministrationClient _adminClient;
        private readonly ServiceBusClient _client;
        private List<EndpointDetails> _endpoints;

        public AzureServiceBusConnection(ConnectionMetadata metadata, ServiceBusAdministrationClient adminClient, ServiceBusClient client)
        {
            Connector = metadata.Connector;
            _metadata = metadata;
            _adminClient = adminClient;
            _client = client;
            _endpoints = metadata.DiscoveredDetails ?? new();
        }

        public IConnector Connector { get; set; }

        public string Name => "AzureServiceBus";

        public int EndpointCount => _endpoints.Count;

        public string Address => _metadata.Address;

        public async Task<List<EndpointDetails>> GetEndpointsAsync()
        {
            var endpoints = new ConcurrentBag<EndpointDetails>();

            var queues = _adminClient.GetQueuesAsync();

            await foreach (var queue in queues)
            {
                endpoints.Add(new EndpointDetails
                {
                    Address = _metadata.Address,
                    Provider = Connector.Provider,
                    Name = queue.Name,
                    Type = "Queue",
                });
            }

            var topics = _adminClient.GetTopicsAsync();

            await foreach (var topic in topics)
            {
                endpoints.Add(new EndpointDetails
                {
                    Address = _metadata.Address,
                    Provider = Connector.Provider,
                    Name = topic.Name,
                    Type = "Topic",
                });
            }

            _endpoints = endpoints.ToList();

            return _endpoints;
        }

        public async Task SendAsync(EndpointDetails endpoint, string json)
        {
            var sender = _client.CreateSender(endpoint.Name);

            await sender.SendMessageAsync(new ServiceBusMessage(json));
        }
    }
}

