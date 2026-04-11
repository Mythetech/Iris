using System;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Iris.Brokers.Models;

namespace Iris.Brokers.Amazon
{
    public class AmazonSimpleQueueServiceConnection : IConnection, IMessageReceiver, IDeadLetterReceiver
    {
        // SQS ReceiveMessage API hard-caps at 10 messages per call.
        public int MaxReceiveBatchSize => 10;

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

        public async Task<IReadOnlyList<ReceivedMessage>> ReceiveAsync(
            EndpointDetails endpoint, int count, CancellationToken cancellationToken = default)
        {
            var queueUrl = (await _client.GetQueueUrlAsync(endpoint.Name, cancellationToken)).QueueUrl;
            return await ReceiveFromUrlAsync(queueUrl, count, ReadSource.Main, cancellationToken);
        }

        public async Task<IReadOnlyList<ReceivedMessage>> ReceiveDeadLetterAsync(
            EndpointDetails endpoint, int count, CancellationToken cancellationToken = default)
        {
            var mainUrl = (await _client.GetQueueUrlAsync(endpoint.Name, cancellationToken)).QueueUrl;
            var attrs = await _client.GetQueueAttributesAsync(
                mainUrl, new List<string> { "RedrivePolicy" }, cancellationToken);

            if (!attrs.Attributes.TryGetValue("RedrivePolicy", out var policyJson)
                || string.IsNullOrWhiteSpace(policyJson))
            {
                // No DLQ configured for this queue — the honest answer to
                // "what's in the DLQ?" is "nothing, because there isn't one."
                // This is a runtime state, not an architectural limitation.
                return Array.Empty<ReceivedMessage>();
            }

            using var doc = JsonDocument.Parse(policyJson);
            var arn = doc.RootElement.GetProperty("deadLetterTargetArn").GetString();
            if (string.IsNullOrWhiteSpace(arn))
                return Array.Empty<ReceivedMessage>();

            // ARN format: arn:aws:sqs:region:account:queuename — the queue
            // name is after the last ':'.
            var dlqName = arn[(arn.LastIndexOf(':') + 1)..];
            var dlqUrl = (await _client.GetQueueUrlAsync(dlqName, cancellationToken)).QueueUrl;
            return await ReceiveFromUrlAsync(dlqUrl, count, ReadSource.DeadLetter, cancellationToken);
        }

        private async Task<IReadOnlyList<ReceivedMessage>> ReceiveFromUrlAsync(
            string queueUrl, int count, ReadSource source, CancellationToken cancellationToken)
        {
            var request = new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = Math.Min(count, MaxReceiveBatchSize),
                WaitTimeSeconds = 1,
                MessageAttributeNames = new List<string> { "All" },
                MessageSystemAttributeNames = new List<string> { "All" },
            };

            var response = await _client.ReceiveMessageAsync(request, cancellationToken);
            var messages = response.Messages ?? new List<Message>();

            // Synthesize ReceiveAndDelete semantics by deleting each message
            // we pulled. SQS has no atomic receive-and-delete.
            if (messages.Count > 0)
            {
                await Task.WhenAll(messages.Select(m =>
                    _client.DeleteMessageAsync(queueUrl, m.ReceiptHandle, cancellationToken)));
            }

            return messages.Select(m => Map(m, source)).ToList();
        }

        private ReceivedMessage Map(Message m, ReadSource source)
        {
            var headers = new Dictionary<string, string>();
            if (m.Attributes is not null)
            {
                foreach (var kvp in m.Attributes)
                    headers[kvp.Key] = kvp.Value ?? string.Empty;
            }

            var properties = new Dictionary<string, string>();
            if (m.MessageAttributes is not null)
            {
                foreach (var kvp in m.MessageAttributes)
                {
                    properties[kvp.Key] = kvp.Value.StringValue
                        ?? kvp.Value.BinaryValue?.ToString()
                        ?? string.Empty;
                }
            }

            int? deliveryCount = null;
            if (headers.TryGetValue("ApproximateReceiveCount", out var rc)
                && int.TryParse(rc, out var parsed))
            {
                deliveryCount = parsed;
            }

            DateTimeOffset? enqueuedTime = null;
            if (headers.TryGetValue("SentTimestamp", out var ts)
                && long.TryParse(ts, out var epochMs))
            {
                enqueuedTime = DateTimeOffset.FromUnixTimeMilliseconds(epochMs);
            }

            return new ReceivedMessage
            {
                Body = m.Body ?? string.Empty,
                MessageId = m.MessageId,
                Headers = headers,
                Properties = properties,
                DeliveryCount = deliveryCount,
                EnqueuedTimeUtc = enqueuedTime,
                Provider = "AmazonSQS",
                Source = source,
                Native = new NativeMessageMetadata
                {
                    ReceiptHandle = m.ReceiptHandle,
                },
            };
        }
    }
}

