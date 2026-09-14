using System;
using System.Net.Http;
using System.Threading;
using EasyNetQ.Management.Client;
using EasyNetQ.Management.Client.Model;
using Iris.Brokers.Extensions;
using Iris.Brokers.Models;
using ConnectorTransports = Iris.Contracts.Brokers.Models.ConnectorTransports;

namespace Iris.Brokers.RabbitMQ
{
    public class RabbitMqConnection : IConnection, IHeaderCarrier, ITransportPropertyCarrier, IMessagePeeker, IMessageReceiver, IDeadLetterPeeker, IDeadLetterReceiver, IEndpointInspector
    {
        // The RabbitMQ HTTP management API's GET-messages endpoint accepts
        // arbitrary counts; 100 is a sane UI cap.
        public int MaxPeekBatchSize => 100;
        public int MaxReceiveBatchSize => 100;

        private readonly ConnectionMetadata _metadata;
        private readonly EasyNetQ.Management.Client.ManagementClient _client;
        private readonly string _address = "";
        private readonly string _vhost;
        private readonly Vhost _vhostRef;
        private List<EndpointDetails> _endpoints;

        public RabbitMqConnection(ConnectionMetadata metadata,
            EasyNetQ.Management.Client.ManagementClient client,
            string vhost = RabbitMqConnector.DefaultVHost)
        {
            Connector = metadata.Connector;
            _vhost = string.IsNullOrWhiteSpace(vhost) ? RabbitMqConnector.DefaultVHost : vhost;

            // Built here rather than fetched with GetVhostAsync on every operation. That call
            // hits GET /api/vhosts/{name}, which the management plugin restricts to users with
            // the administrator tag, so a plain management user could not peek, read, inspect or
            // discover anything. The client only ever uses the name to build its request path.
            _vhostRef = new Vhost(_vhost, Tracing: false);
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

        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>The virtual host every operation on this connection is scoped to.</summary>
        public string VHost => _vhost;

        public string Name { get; set; } = ConnectorTransports.RabbitMq;

        public string Address => _address;

        public int EndpointCount => _endpoints.Count;

        /// <summary>
        /// Scoped to this connection's virtual host. Listing across every vhost showed queues
        /// that no send, read or inspect on this connection could reach.
        /// </summary>
        public async Task<List<EndpointDetails>> GetEndpointsAsync()
        {
            var queues = await _client.GetQueuesAsync(_vhostRef);

            var endpoints = queues.Select(x => new EndpointDetails
            {
                Address = _client.Endpoint.GetLeftPart(UriPartial.Authority),
                Name = x.Name,
                Type = "Queue",
                Provider = Connector.Provider,
            })
             .ToList();

            var exchanges = await _client.GetExchangesAsync(_vhostRef);

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

        public int MaxHeaderCount => int.MaxValue;

        public bool IsValidHeaderKey(string key) => !string.IsNullOrWhiteSpace(key);

        public IReadOnlySet<HeaderDataType> SupportedDataTypes { get; } =
            new HashSet<HeaderDataType>(Enum.GetValues<HeaderDataType>());

        public IReadOnlySet<TransportProperty> SupportedProperties { get; } =
            new HashSet<TransportProperty>(Enum.GetValues<TransportProperty>());

        public async Task SendAsync(EndpointDetails endpoint, MessageRequest message)
        {
            // The management API honours only the standard AMQP basic-property names at the
            // top level of `properties`; anything else there is silently dropped. Application
            // headers therefore go under `properties.headers`, and the typed transport
            // properties are lifted to the top level where consumers such as EasyNetQ and
            // Wolverine read them.
            var headers = message.Headers.ToDictionary(
                kvp => kvp.Key,
                kvp => EncodeForManagementApi(kvp.Value, message.HeaderTypeOf(kvp.Key)));

            var properties = new Dictionary<string, object?>
            {
                ["headers"] = headers,
            };

            var transport = message.TransportProperties;
            if (transport.MessageId is not null) properties["message_id"] = transport.MessageId;
            if (transport.CorrelationId is not null) properties["correlation_id"] = transport.CorrelationId;
            if (transport.ContentType is not null) properties["content_type"] = transport.ContentType;
            if (transport.Type is not null) properties["type"] = transport.Type;
            if (transport.Timestamp is not null) properties["timestamp"] = transport.Timestamp.Value.ToUnixTimeSeconds();
            if (transport.Persistent is not null) properties["delivery_mode"] = transport.Persistent.Value ? 2 : 1;

            var exchange = endpoint?.Type?.Equals("queue", StringComparison.OrdinalIgnoreCase) ?? true
                ? "amq.default"
                : endpoint!.Name;

            await _client.PublishAsync(_vhost, exchange,
                new PublishInfo(endpoint?.Name ?? "/", message.Json, Properties: properties));
        }

        // AMQP has no timestamp header type the management API will accept from JSON, so
        // timestamps stay ISO 8601 strings; numbers and booleans become native JSON values.
        private static object? EncodeForManagementApi(string value, HeaderDataType type)
            => type == HeaderDataType.Timestamp ? value : HeaderValueEncoder.Encode(value, type);

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
            var queue = await _client.GetQueueAsync(
                _vhostRef, sourceQueueName, cancellationToken: cancellationToken);

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
                _vhostRef, dlxName, cancellationToken);

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
            var criteria = new GetMessagesFromQueueInfo(count, ackMode, "auto");
            var messages = await _client.GetMessagesFromQueueAsync(
                _vhostRef, endpoint.Name, criteria, cancellationToken);
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

        public async Task<Iris.Contracts.Brokers.Models.EndpointPropertiesDto> InspectAsync(
            string endpointName,
            string? type,
            CancellationToken cancellationToken = default)
        {
            var entries = new List<Iris.Contracts.Brokers.Models.EndpointPropertyEntry>();

            if (string.Equals(type, "Exchange", StringComparison.OrdinalIgnoreCase))
            {
                var exchange = await _client.GetExchangeAsync(
                    _vhostRef, endpointName, cancellationToken: cancellationToken);

                entries.Add(new("Type", exchange.Type));
                entries.Add(new("Vhost", exchange.Vhost));
                entries.Add(new("Durable", exchange.Durable.ToString()));
                entries.Add(new("Auto Delete", exchange.AutoDelete.ToString()));
                entries.Add(new("Internal", exchange.Internal.ToString()));

                if (exchange.Arguments is { Count: > 0 })
                    entries.Add(new("Arguments", FormatArguments(exchange.Arguments)));

                var bindings = await _client.GetBindingsWithSourceAsync(
                    _vhostRef, endpointName, cancellationToken);
                entries.Add(new("Bindings", FormatBindings(bindings, fromExchange: true)));
            }
            else
            {
                var queue = await _client.GetQueueAsync(
                    _vhostRef, endpointName, cancellationToken: cancellationToken);

                entries.Add(new("Vhost", queue.Vhost));
                entries.Add(new("State", queue.State));
                entries.Add(new("Node", queue.Node));
                entries.Add(new("Messages", queue.Messages.ToString()));
                entries.Add(new("Messages Ready", queue.MessagesReady.ToString()));
                entries.Add(new("Messages Unacknowledged", queue.MessagesUnacknowledged.ToString()));
                entries.Add(new("Consumers", queue.Consumers.ToString()));
                entries.Add(new("Durable", queue.Durable.ToString()));
                entries.Add(new("Auto Delete", queue.AutoDelete.ToString()));
                entries.Add(new("Exclusive", queue.Exclusive.ToString()));
                entries.Add(new("Memory", queue.Memory.ToString()));

                if (!string.IsNullOrWhiteSpace(queue.Policy))
                    entries.Add(new("Policy", queue.Policy));

                if (queue.Arguments is not null)
                {
                    if (queue.Arguments.TryGetValue("x-dead-letter-exchange", out var dlx) && dlx is not null)
                        entries.Add(new("Dead Letter Exchange", dlx.ToString()));
                    if (queue.Arguments.TryGetValue("x-dead-letter-routing-key", out var dlrk) && dlrk is not null)
                        entries.Add(new("Dead Letter Routing Key", dlrk.ToString()));
                    if (queue.Arguments.TryGetValue("x-message-ttl", out var ttl) && ttl is not null)
                        entries.Add(new("Message TTL (ms)", ttl.ToString()));
                    if (queue.Arguments.TryGetValue("x-max-length", out var maxLen) && maxLen is not null)
                        entries.Add(new("Max Length", maxLen.ToString()));
                }

                var bindings = await _client.GetBindingsForQueueAsync(
                    _vhostRef, endpointName, cancellationToken);
                entries.Add(new("Bindings", FormatBindings(bindings, fromExchange: false)));
            }

            return new Iris.Contracts.Brokers.Models.EndpointPropertiesDto(entries);
        }

        private static string FormatBindings(IReadOnlyList<Binding> bindings, bool fromExchange)
        {
            if (bindings is null || bindings.Count == 0)
                return "(none)";

            // For an exchange we list its destinations; for a queue we list its sources.
            return string.Join("; ", bindings.Select(b =>
            {
                var label = fromExchange
                    ? $"→ {b.DestinationType}:{b.Destination}"
                    : $"{b.Source} →";
                return string.IsNullOrEmpty(b.RoutingKey)
                    ? label
                    : $"{label} ({b.RoutingKey})";
            }));
        }

        private static string FormatArguments(IReadOnlyDictionary<string, object> args)
            => string.Join(", ", args.Select(kvp => $"{kvp.Key}={kvp.Value}"));
    }
}


