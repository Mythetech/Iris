using System;
using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Iris.Brokers.Models;

namespace Iris.Brokers.Azure
{
    public class AzureServiceBusConnection : IConnection, IMessagePeeker, IMessageReceiver, IDeadLetterPeeker, IDeadLetterReceiver
    {
        // ServiceBusReceiver's practical batch ceiling is around 250 messages.
        public int MaxPeekBatchSize => 250;
        public int MaxReceiveBatchSize => 250;

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

        public Task<IReadOnlyList<ReceivedMessage>> PeekAsync(
            EndpointDetails endpoint, int count, CancellationToken cancellationToken = default)
            => PeekFromAsync(endpoint, count, SubQueue.None, ReadSource.Main, cancellationToken);

        public Task<IReadOnlyList<ReceivedMessage>> ReceiveAsync(
            EndpointDetails endpoint, int count, CancellationToken cancellationToken = default)
            => ReceiveFromAsync(endpoint, count, SubQueue.None, ReadSource.Main, cancellationToken);

        public Task<IReadOnlyList<ReceivedMessage>> PeekDeadLetterAsync(
            EndpointDetails endpoint, int count, CancellationToken cancellationToken = default)
            => PeekFromAsync(endpoint, count, SubQueue.DeadLetter, ReadSource.DeadLetter, cancellationToken);

        public Task<IReadOnlyList<ReceivedMessage>> ReceiveDeadLetterAsync(
            EndpointDetails endpoint, int count, CancellationToken cancellationToken = default)
            => ReceiveFromAsync(endpoint, count, SubQueue.DeadLetter, ReadSource.DeadLetter, cancellationToken);

        private async Task<IReadOnlyList<ReceivedMessage>> PeekFromAsync(
            EndpointDetails endpoint, int count, SubQueue subQueue, ReadSource source, CancellationToken cancellationToken)
        {
            await using var receiver = _client.CreateReceiver(endpoint.Name, new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
                SubQueue = subQueue,
            });

            var peeked = await receiver.PeekMessagesAsync(count, fromSequenceNumber: null, cancellationToken);
            return peeked.Select(m => Map(m, source)).ToList();
        }

        private async Task<IReadOnlyList<ReceivedMessage>> ReceiveFromAsync(
            EndpointDetails endpoint, int count, SubQueue subQueue, ReadSource source, CancellationToken cancellationToken)
        {
            await using var receiver = _client.CreateReceiver(endpoint.Name, new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
                SubQueue = subQueue,
            });

            var received = await receiver.ReceiveMessagesAsync(count, maxWaitTime: TimeSpan.FromSeconds(2), cancellationToken);
            return received.Select(m => Map(m, source)).ToList();
        }

        private ReceivedMessage Map(ServiceBusReceivedMessage m, ReadSource source)
        {
            var headers = new Dictionary<string, string>();
            foreach (var kvp in m.ApplicationProperties)
                headers[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;

            var bodyBytes = m.Body.ToArray();

            return new ReceivedMessage
            {
                Body = m.Body.ToString(),
                MessageId = m.MessageId,
                CorrelationId = m.CorrelationId,
                ContentType = m.ContentType,
                Headers = headers,
                DeliveryCount = m.DeliveryCount,
                EnqueuedTimeUtc = m.EnqueuedTime,
                ExpiresAtUtc = m.ExpiresAt,
                SizeInBytes = bodyBytes.LongLength,
                Provider = "AzureServiceBus",
                Source = source,
                Native = new NativeMessageMetadata
                {
                    // LockToken is populated by SB even under ReceiveAndDelete mode;
                    // we retain it for diagnostic display only — it's not used for ack flow.
                    LockToken = m.LockToken,
                },
            };
        }
    }
}

