using System;
using System.Net.Http;
using System.Threading;
using EasyNetQ.Management.Client;
using EasyNetQ.Management.Client.Model;
using Iris.Brokers.Extensions;
using Iris.Brokers.Models;

namespace Iris.Brokers.RabbitMQ
{
    // Note: IMessageReader implementation is re-added in slice S1 of the
    // broker-read-side feature, using the new formal contract.
    public class RabbitMqConnection : IConnection
    {
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
    }
}


