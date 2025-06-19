using System;
using System.Net.Http;
using System.Threading;
using EasyNetQ.Management.Client;
using EasyNetQ.Management.Client.Model;
using Iris.Brokers.Extensions;
using Iris.Brokers.Models;

namespace Iris.Brokers.RabbitMQ
{
    public class RabbitMqConnection : IConnection, IMessageReader
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

        public async Task ReadAsync(string messageId)
        {
            var message = await _client.GetMessagesFromQueueAsync(Rabbit.VHost ?? "", messageId, new GetMessagesFromQueueInfo(100, AckMode.AckRequeueFalse));
        }

        public async Task SendAsync(EndpointDetails endpoint, MessageRequest message)
        {
            var result = await _client.PublishAsync($"{Rabbit.VHost}", 
                    endpoint?.Type?.Equals("queue", StringComparison.OrdinalIgnoreCase) ?? true ? "amq.default" : endpoint.Name, 
                    new PublishInfo(endpoint?.Name ?? "/", message.Json, Properties: message.Headers.ToReadOnly()!));
        }

        public async Task SendAsync(EndpointDetails endpoint, string json)
        {
            Console.WriteLine(json);

            var result = await _client.PublishAsync($"{Rabbit.VHost}", endpoint?.Type?.Equals("queue", StringComparison.OrdinalIgnoreCase) ?? true ? "amq.default" : endpoint.Name, new PublishInfo(endpoint?.Name ?? "/", json));
        }
    }
}


