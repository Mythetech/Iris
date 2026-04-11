using System;
using System.Net.Http;
using System.Threading;
using EasyNetQ.Management.Client;
using EasyNetQ.Management.Client.Model;
using Iris.Brokers.Extensions;
using Iris.Brokers.Models;

namespace Iris.Brokers.RabbitMQ
{
    public class RabbitMqConnection : IConnection, IMessagePeeker, IMessageReceiver, IDeadLetterPeeker, IDeadLetterReceiver
    {
        // The RabbitMQ HTTP management API's GET-messages endpoint accepts
        // arbitrary counts; 100 is a sane UI cap.
        public int MaxPeekBatchSize => 100;
        public int MaxReceiveBatchSize => 100;

        private readonly ConnectionMetadata _metadata;
        private readonly EasyNetQ.Management.Client.ManagementClient _client;
        private readonly string _address = "";
        private List<EndpointDetails> _endpoints;

        public RabbitMqConnection(ConnectionMetadata metadata,
            EasyNetQ.Management.Client.ManagementClient client)
        {
            Connector = metadata.Connector;
            _address = client.Endpoint.GetLeftPart(UriPartial.Authority);
            _metadata = metadata;
            _client = client;
            _endpoints = metadata.DiscoveredDetails ?? new();

            if (_address.Contains("localhost"))
                Name = "Docker";
            else if (_address.Contains("cloudamqp", StringComparison.OrdinalIgnoreCase))
                Name = "CloudAmpq";
        }

        public IConnector Connector { get; set; }

        private RabbitMqConnector Rabbit => (RabbitMqConnector)Connector;

        public string Name { get; set; } = "RabbitMq";

        public string Address => _address;

        public int EndpointCount => _endpoints.Count;

        public async Task<List<EndpointDetails>> GetEndpointsAsync()
        {
            var queues = await _client.GetQueuesAsync();

            var endpoints = queues.Select(x => new EndpointDetails
            {
                Address = _client.Endpoint.GetLeftPart(UriPartial.Authority),
                Name = x.Name,
                Type = "Queue",
                Provider = Connector.Provider,
            })
             .ToList();

            var exchanges = await _client.GetExchangesAsync();

            endpoints.AddRange(exchanges.Select(x => new EndpointDetails
            {
                Address = _client.Endpoint.GetLeftPart(UriPartial.Authority),
                Name = x.Name.Length > 0 ? x.Name : "/",
                Type = "Exchange",
                Provider = Connector.Provider,
            }));

            _endpoints = endpoints;

            return endpoints;
        }

        public async Task SendAsync(EndpointDetails endpoint, MessageRequest message)
        {
            // RabbitMQ's HTTP management API only honors a fixed set of standard AMQP basic-property
            // names at the top level of `properties` (message_id, correlation_id, content_type, ...)
            // - anything else is silently dropped. Custom headers (e.g. Rebus's rbs2-* keys, the
            // iris-key tracker, MassTransit-set headers) must be nested under `properties.headers`
            // so they reach the consumer as AMQP application headers.
            var properties = new Dictionary<string, object?>
            {
                ["headers"] = message.Headers.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value),
            };

            var result = await _client.PublishAsync($"{Rabbit.VHost}",
                    endpoint?.Type?.Equals("queue", StringComparison.OrdinalIgnoreCase) ?? true ? "amq.default" : endpoint.Name,
                    new PublishInfo(endpoint?.Name ?? "/", message.Json, Properties: properties));
        }

        public async Task SendAsync(EndpointDetails endpoint, string json)
        {
            var result = await _client.PublishAsync($"{Rabbit.VHost}", endpoint?.Type?.Equals("queue", StringComparison.OrdinalIgnoreCase) ?? true ? "amq.default" : endpoint.Name, new PublishInfo(endpoint?.Name ?? "/", json));
        }

        public Task<IReadOnlyList<ReceivedMessage>> PeekAsync(
            EndpointDetails endpoint, int count, CancellationToken cancellationToken = default)
            => GetMessagesAsync(endpoint, count, AckMode.AckRequeueTrue, cancellationToken);

        public Task<IReadOnlyList<ReceivedMessage>> ReceiveAsync(
            EndpointDetails endpoint, int count, CancellationToken cancellationToken = default)
            => GetMessagesAsync(endpoint, count, AckMode.AckRequeueFalse, cancellationToken);

        public async Task<IReadOnlyList<ReceivedMessage>> PeekDeadLetterAsync(
            EndpointDetails endpoint, int count, CancellationToken cancellationToken = default)
        {
            var dlqEndpoint = await BuildDlqEndpointAsync(endpoint, cancellationToken);
            if (dlqEndpoint is null)
                return Array.Empty<ReceivedMessage>();

            return await GetMessagesAsync(
                dlqEndpoint, count, AckMode.AckRequeueTrue, cancellationToken, ReadSource.DeadLetter);
        }

        public async Task<IReadOnlyList<ReceivedMessage>> ReceiveDeadLetterAsync(
            EndpointDetails endpoint, int count, CancellationToken cancellationToken = default)
        {
            var dlqEndpoint = await BuildDlqEndpointAsync(endpoint, cancellationToken);
            if (dlqEndpoint is null)
                return Array.Empty<ReceivedMessage>();

            return await GetMessagesAsync(
                dlqEndpoint, count, AckMode.AckRequeueFalse, cancellationToken, ReadSource.DeadLetter);
        }

        private async Task<EndpointDetails?> BuildDlqEndpointAsync(
            EndpointDetails source, CancellationToken cancellationToken)
        {
            var dlqName = await ResolveDlqNameAsync(source.Name, cancellationToken);
            if (dlqName is null)
                return null;

            return new EndpointDetails
            {
                Address = source.Address,
                Provider = source.Provider,
                Type = source.Type,
                Name = dlqName,
            };
        }

        /// <summary>
        /// Resolves the "dead-letter queue" for a RabbitMQ source queue by:
        /// (1) reading the source queue's <c>x-dead-letter-exchange</c> argument,
        /// (2) asking the broker which queues are bound to that exchange, and
        /// (3) picking the binding whose routing key matches the source queue's
        /// <c>x-dead-letter-routing-key</c> if set, otherwise the first queue binding.
        /// Returns <c>null</c> when the source queue has no DLX configured —
        /// i.e. when dead-lettering is simply not set up for this queue. That's
        /// a runtime configuration state, not an architectural limitation, and
        /// is mirrored by the SQS RedrivePolicy path in
        /// <see cref="Iris.Brokers.Amazon.AmazonSimpleQueueServiceConnection"/>.
        /// </summary>
        private async Task<string?> ResolveDlqNameAsync(
            string sourceQueueName, CancellationToken cancellationToken)
        {
            var vhost = await _client.GetVhostAsync(Rabbit.VHost, cancellationToken);
            var queue = await _client.GetQueueAsync(
                vhost, sourceQueueName, cancellationToken: cancellationToken);

            if (!queue.Arguments.TryGetValue("x-dead-letter-exchange", out var dlxValue)
                || dlxValue is null)
            {
                return null;
            }

            var dlxName = dlxValue.ToString();
            if (string.IsNullOrWhiteSpace(dlxName))
                return null;

            string? dlxRoutingKey = null;
            if (queue.Arguments.TryGetValue("x-dead-letter-routing-key", out var rkValue)
                && rkValue is not null)
            {
                dlxRoutingKey = rkValue.ToString();
            }

            var bindings = await _client.GetBindingsWithSourceAsync(
                vhost, dlxName, cancellationToken);

            var queueBindings = bindings
                .Where(b => string.Equals(b.DestinationType, "queue", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (queueBindings.Count == 0)
                return null;

            if (!string.IsNullOrEmpty(dlxRoutingKey))
            {
                var match = queueBindings.FirstOrDefault(
                    b => string.Equals(b.RoutingKey, dlxRoutingKey, StringComparison.Ordinal));
                if (match is not null)
                    return match.Destination;
            }

            return queueBindings[0].Destination;
        }

        private async Task<IReadOnlyList<ReceivedMessage>> GetMessagesAsync(
            EndpointDetails endpoint,
            int count,
            AckMode ackMode,
            CancellationToken cancellationToken,
            ReadSource source = ReadSource.Main)
        {
            var vhost = await _client.GetVhostAsync(Rabbit.VHost, cancellationToken);
            var criteria = new GetMessagesFromQueueInfo(count, ackMode, "auto");
            var messages = await _client.GetMessagesFromQueueAsync(
                vhost, endpoint.Name, criteria, cancellationToken);
            return messages.Select(m => Map(m) with { Source = source }).ToList();
        }

        private ReceivedMessage Map(Message msg)
        {
            // Message.Properties is IReadOnlyDictionary<string, object> — a loosely
            // typed bag of AMQP basic-properties. Pull well-known fields out into
            // strongly-typed slots; keep the rest as headers.
            var allProperties = msg.Properties;

            string? messageId = null;
            string? correlationId = null;
            string? contentType = null;
            var headers = new Dictionary<string, string>();

            foreach (var kvp in allProperties)
            {
                switch (kvp.Key)
                {
                    case "message_id":
                        messageId = kvp.Value?.ToString();
                        break;
                    case "correlation_id":
                        correlationId = kvp.Value?.ToString();
                        break;
                    case "content_type":
                        contentType = kvp.Value?.ToString();
                        break;
                    case "headers" when kvp.Value is IReadOnlyDictionary<string, object?> nested:
                        foreach (var header in nested)
                            headers[header.Key] = header.Value?.ToString() ?? string.Empty;
                        break;
                    case "headers" when kvp.Value is IDictionary<string, object?> nestedMut:
                        foreach (var header in nestedMut)
                            headers[header.Key] = header.Value?.ToString() ?? string.Empty;
                        break;
                    default:
                        if (kvp.Value is not null)
                            headers[kvp.Key] = kvp.Value.ToString() ?? string.Empty;
                        break;
                }
            }

            var properties = new Dictionary<string, string>
            {
                ["routing_key"] = msg.RoutingKey ?? string.Empty,
                ["exchange"] = msg.Exchange ?? string.Empty,
                ["redelivered"] = msg.Redelivered.ToString(),
            };

            if (string.Equals(msg.PayloadEncoding, "base64", StringComparison.OrdinalIgnoreCase))
                headers["iris.payload_encoding"] = "base64";

            return new ReceivedMessage
            {
                Body = msg.Payload ?? string.Empty,
                MessageId = messageId,
                CorrelationId = correlationId,
                ContentType = contentType,
                Headers = headers,
                Properties = properties,
                SizeInBytes = msg.PayloadBytes,
                Provider = "RabbitMQ",
                Source = Models.ReadSource.Main,
                Native = new NativeMessageMetadata
                {
                    RoutingKey = msg.RoutingKey,
                    Exchange = msg.Exchange,
                },
            };
        }
    }
}


