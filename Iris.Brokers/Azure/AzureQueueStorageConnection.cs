using System;
using Azure.Storage.Queues;
using Iris.Brokers.Models;
using Microsoft.Extensions.Logging;

namespace Iris.Brokers.Azure
{
    public class AzureQueueStorageConnection : IConnection
    {
        private readonly ConnectionMetadata _metadata;
        private readonly QueueServiceClient _queueClient;
        private List<EndpointDetails> _endpoints;
        private ILogger<AzureQueueStorageConnection> _logger;

        public AzureQueueStorageConnection(ConnectionMetadata metadata, QueueServiceClient queueClient, ILogger<AzureQueueStorageConnection> logger)
        {
            _metadata = metadata;
            _queueClient = queueClient;
            Connector = metadata.Connector;
            _logger = logger;
            _endpoints = metadata.DiscoveredDetails ?? new();
        }

        public IConnector Connector { get; set; }

        public string Name => "AzureQueueStorage";

        public string Address => _queueClient.Uri.GetLeftPart(UriPartial.Authority);

        public int EndpointCount => _endpoints.Count;

        public async Task<List<EndpointDetails>> GetEndpointsAsync()
        {
            var endpoints = await _queueClient.GetQueuesAsync().ToListAsync();

            return _endpoints = endpoints.Select(x => new EndpointDetails()
            {
                Address = Address,
                Name = x.Name,
                Provider = Connector.Provider,
                Type = "Queue",
            }).ToList();
        }

        public async Task SendAsync(EndpointDetails endpoint, string json)
        {
            var sender = _queueClient.GetQueueClient(endpoint.Name);

            try
            {
                var response = await sender.SendMessageAsync(json);
                _logger.LogInformation("{MessageId} message sent successfully to {Endpoint}", response.Value.MessageId, endpoint.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to Azure Queue Storage {Endpoint}", endpoint.Name);
            }

        }
    }
}

