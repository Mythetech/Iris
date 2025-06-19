using System;
using Amazon.SQS;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Iris.Brokers.Models;

namespace Iris.Brokers.Amazon
{
    public class AmazonSimpleQueueServiceConnection : IConnection
    {
        private readonly ConnectionMetadata _metadata;
        private readonly AmazonSQSClient _client;
        private List<EndpointDetails> _endpoints;

        public AmazonSimpleQueueServiceConnection(ConnectionMetadata metadata, AmazonSQSClient client)
        {
            Connector = metadata.Connector;
            _metadata = metadata;
            _client = client;
            _endpoints = metadata.DiscoveredDetails ?? new();
        }

        public IConnector Connector { get; set; }

        public string Name => "SimpleQueueService";

        public int EndpointCount => _endpoints.Count;

        public string Address => _metadata.Address;

        public async Task<List<EndpointDetails>> GetEndpointsAsync()
        {
            var queueResponse = await _client.ListQueuesAsync("");

            var endpoints = queueResponse.QueueUrls.Select(x => new EndpointDetails()
            {
                Address = x.LastIndexOf('/') > 0 ? x[..x.LastIndexOf('/')] : x,
                Name = x.LastIndexOf('/') > 0 ? x[(x.LastIndexOf('/')+1)..] : x,
                Provider = Connector.Provider,
                Type = "Queue"
            });

            return _endpoints = endpoints.ToList();
        }

        public async Task SendAsync(EndpointDetails endpoint, string json)
        {
            var response = await _client.SendMessageAsync(endpoint.Name, json, CancellationToken.None);
        }
    }
}

