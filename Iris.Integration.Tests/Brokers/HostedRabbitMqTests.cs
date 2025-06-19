using System;
using System.Collections.Generic;
using EasyNetQ.Management.Client;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Integration.Tests.Brokers
{
    public class HostedRabbitMqTests : IClassFixture<IrisWebApplicationFactory>, IAsyncLifetime
    {
        private readonly IrisWebApplicationFactory _factory;

        private string QueueName = "changecolorscommand";

        private ConnectionData _data = new()
        {
            Uri = "https://shrimp.rmq.cloudamqp.com",
            Username = "rlbrfkzc",
            Password = "A6qCDiyOP7iQrZHZV5dRS6rMbQE8NxxB",
        };

        public HostedRabbitMqTests(IrisWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact(DisplayName = "Can connect to hosted RabbitMq and retrieve endpoints")]
        public async Task Can_ConnectTo_HostedRabbitMq()
        {
            // Arrange
            var data = _data;

            var connectors = _factory.Services.GetRequiredService<IEnumerable<IConnector>>();
            var connector = connectors?.FirstOrDefault(x => x.Provider.Equals("rabbitmq", StringComparison.OrdinalIgnoreCase));
            connector.Should().NotBeNull();

            // Act
            var connection = await connector!.ConnectAsync(data);
            var endpoints = await connection.GetEndpointsAsync();

            // Assert
            connection.Should().NotBeNull();
            connection!.Address.Should().ContainEquivalentOf(data.Uri[8..]);
            connection!.EndpointCount.Should().BeGreaterThan(0);
            endpoints.Count.Should().BeGreaterThan(0);
            connection!.EndpointCount.Should().Be(endpoints.Count);
            endpoints.Any(x => x.Type.Equals("queue", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
            endpoints.Any(x => x.Type.Equals("exchange", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        }

        [Fact(DisplayName = "Can send message to hosted Rabbit")]
        public async Task Can_SendMessageTo_HostedRabbitMq()
        {
            // Arrange
            var data = _data;

            var connectors = _factory.Services.GetRequiredService<IEnumerable<IConnector>>();
            var connector = connectors?.FirstOrDefault(x => x.Provider.Equals("rabbitmq", StringComparison.OrdinalIgnoreCase));
            connector.Should().NotBeNull();

            // Act
            var connection = await connector!.ConnectAsync(data, false);

            Func<Task> act = async () =>
            {
                await connection.SendAsync(new EndpointDetails()
                {
                    Type = "queue",
                    Provider = "rabbitmq",
                    Name = QueueName,
                    Address = QueueName
                }, "{\"Red\": 0, \"Green\": 20, \"Blue\": 30}");
            };



            // Assert
            connection.Should().NotBeNull();
            connection!.Address.Should().ContainEquivalentOf(data.Uri[8..]);
            await act.Should().NotThrowAsync();
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task InitializeAsync()
        {
            var client = new ManagementClient(new Uri(_data.Uri), _data.Username, _data.Password);

            try
            {
                var queue = await client.GetQueueAsync(_data.Username, QueueName);
                Console.WriteLine("RabbitMq hosted test queue exists");
            }
            catch (UnexpectedHttpStatusCodeException ex)
            {
                Console.WriteLine($"Recreating {QueueName} on CloudAMPQ hosted environment...");
                await client.CreateQueueAsync(_data.Username, QueueName, new());
                Console.WriteLine($"Created {QueueName}");
            }
        }
    }
}

