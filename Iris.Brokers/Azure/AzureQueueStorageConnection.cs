using System;
using Azure.Storage.Queues;
using Iris.Brokers.Models;
using Microsoft.Extensions.Logging;

namespace Iris.Brokers.Azure
{
    public class AzureQueueStorageConnection : IConnection, IMessagePeeker, IMessageReceiver
    {
        // Azure Queue Storage hard-caps peek/receive at 32 messages per call.
        public int MaxPeekBatchSize => 32;
        public int MaxReceiveBatchSize => 32;

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

        public async Task<IReadOnlyList<ReceivedMessage>> PeekAsync(
            EndpointDetails endpoint, int count, CancellationToken cancellationToken = default)
        {
            var queue = _queueClient.GetQueueClient(endpoint.Name);
            var response = await queue.PeekMessagesAsync(
                maxMessages: Math.Min(count, MaxPeekBatchSize), cancellationToken);

            return response.Value.Select(m => Map(
                messageId: m.MessageId,
                body: m.Body?.ToString() ?? string.Empty,
                insertedOn: m.InsertedOn,
                expiresOn: m.ExpiresOn,
                dequeueCount: null,
                popReceipt: null)).ToList();
        }

        public async Task<IReadOnlyList<ReceivedMessage>> ReceiveAsync(
            EndpointDetails endpoint, int count, CancellationToken cancellationToken = default)
        {
            // Azure Queue Storage has no atomic ReceiveAndDelete — synthesize it
            // by receiving with a 30s visibility window, then deleting each
            // message before returning. The visibility window is the "honor
            // visibility timeout" requirement: during the delete window, a
            // competing consumer cannot re-dequeue the same message.
            var queue = _queueClient.GetQueueClient(endpoint.Name);
            var response = await queue.ReceiveMessagesAsync(
                maxMessages: Math.Min(count, MaxReceiveBatchSize),
                visibilityTimeout: TimeSpan.FromSeconds(30),
                cancellationToken: cancellationToken);

            var received = response.Value;

            await Task.WhenAll(received.Select(m =>
                queue.DeleteMessageAsync(m.MessageId, m.PopReceipt, cancellationToken)));

            return received.Select(m => Map(
                messageId: m.MessageId,
                body: m.Body?.ToString() ?? string.Empty,
                insertedOn: m.InsertedOn,
                expiresOn: m.ExpiresOn,
                dequeueCount: (int?)m.DequeueCount,
                popReceipt: m.PopReceipt)).ToList();
        }

        private ReceivedMessage Map(
            string messageId,
            string body,
            DateTimeOffset? insertedOn,
            DateTimeOffset? expiresOn,
            int? dequeueCount,
            string? popReceipt)
        {
            return new ReceivedMessage
            {
                Body = body,
                MessageId = messageId,
                DeliveryCount = dequeueCount,
                EnqueuedTimeUtc = insertedOn,
                ExpiresAtUtc = expiresOn,
                Provider = "AzureQueueStorage",
                Source = ReadSource.Main,
                Native = new NativeMessageMetadata
                {
                    PopReceipt = popReceipt,
                },
            };
        }
    }
}

