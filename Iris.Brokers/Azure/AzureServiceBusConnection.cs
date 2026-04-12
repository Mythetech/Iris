using System;
using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Iris.Brokers.Models;

namespace Iris.Brokers.Azure
{
    public class AzureServiceBusConnection : IConnection, IMessagePeeker, IMessageReceiver, IDeadLetterPeeker, IDeadLetterReceiver, IEndpointInspector
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

        public Guid Id { get; } = Guid.NewGuid();

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

        public async Task<Iris.Contracts.Brokers.Models.EndpointPropertiesDto> InspectAsync(
            string endpointName,
            string? type,
            CancellationToken cancellationToken = default)
        {
            var entries = new List<Iris.Contracts.Brokers.Models.EndpointPropertyEntry>();

            if (string.Equals(type, "Topic", StringComparison.OrdinalIgnoreCase))
            {
                var topic = (await _adminClient.GetTopicAsync(endpointName, cancellationToken)).Value;
                var runtime = (await _adminClient.GetTopicRuntimePropertiesAsync(endpointName, cancellationToken)).Value;

                entries.Add(new("Status", topic.Status.ToString()));
                entries.Add(new("Subscription Count", runtime.SubscriptionCount.ToString()));
                entries.Add(new("Scheduled Messages", runtime.ScheduledMessageCount.ToString()));
                entries.Add(new("Size (bytes)", runtime.SizeInBytes.ToString()));
                entries.Add(new("Max Size (MB)", topic.MaxSizeInMegabytes.ToString()));
                entries.Add(new("Default TTL", topic.DefaultMessageTimeToLive.ToString()));
                entries.Add(new("Auto Delete On Idle", topic.AutoDeleteOnIdle.ToString()));
                entries.Add(new("Requires Duplicate Detection", topic.RequiresDuplicateDetection.ToString()));
                entries.Add(new("Enable Partitioning", topic.EnablePartitioning.ToString()));
                entries.Add(new("Support Ordering", topic.SupportOrdering.ToString()));
                entries.Add(new("Created At", runtime.CreatedAt.ToString("u")));
                entries.Add(new("Updated At", runtime.UpdatedAt.ToString("u")));
            }
            else
            {
                var queue = (await _adminClient.GetQueueAsync(endpointName, cancellationToken)).Value;
                var runtime = (await _adminClient.GetQueueRuntimePropertiesAsync(endpointName, cancellationToken)).Value;

                entries.Add(new("Status", queue.Status.ToString()));
                entries.Add(new("Active Messages", runtime.ActiveMessageCount.ToString()));
                entries.Add(new("Dead Letter Messages", runtime.DeadLetterMessageCount.ToString()));
                entries.Add(new("Scheduled Messages", runtime.ScheduledMessageCount.ToString()));
                entries.Add(new("Transfer Messages", runtime.TransferMessageCount.ToString()));
                entries.Add(new("Total Messages", runtime.TotalMessageCount.ToString()));
                entries.Add(new("Size (bytes)", runtime.SizeInBytes.ToString()));
                entries.Add(new("Max Size (MB)", queue.MaxSizeInMegabytes.ToString()));
                entries.Add(new("Max Delivery Count", queue.MaxDeliveryCount.ToString()));
                entries.Add(new("Lock Duration", queue.LockDuration.ToString()));
                entries.Add(new("Default TTL", queue.DefaultMessageTimeToLive.ToString()));
                entries.Add(new("Auto Delete On Idle", queue.AutoDeleteOnIdle.ToString()));
                entries.Add(new("Requires Session", queue.RequiresSession.ToString()));
                entries.Add(new("Requires Duplicate Detection", queue.RequiresDuplicateDetection.ToString()));
                entries.Add(new("Dead Letter On Expiration", queue.DeadLetteringOnMessageExpiration.ToString()));
                entries.Add(new("Enable Partitioning", queue.EnablePartitioning.ToString()));
                if (!string.IsNullOrEmpty(queue.ForwardTo))
                    entries.Add(new("Forward To", queue.ForwardTo));
                if (!string.IsNullOrEmpty(queue.ForwardDeadLetteredMessagesTo))
                    entries.Add(new("Forward DLQ To", queue.ForwardDeadLetteredMessagesTo));
                entries.Add(new("Created At", runtime.CreatedAt.ToString("u")));
                entries.Add(new("Updated At", runtime.UpdatedAt.ToString("u")));
            }

            return new Iris.Contracts.Brokers.Models.EndpointPropertiesDto(entries);
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

