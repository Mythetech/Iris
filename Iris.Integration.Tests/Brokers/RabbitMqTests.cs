using System;
using System.Collections.Immutable;
using FastEndpoints;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Models;
using Iris.Brokers.RabbitMQ;
using Testcontainers.RabbitMq;

namespace Iris.Integration.Tests.Brokers
{
    public class RabbitMqTests : IAsyncLifetime
    {
        private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
                .WithPortBinding(15672)
                .WithExposedPort(15672)
                .WithImage("rabbitmq:3-management")
                .WithUsername("guest")
                .WithPassword("guest")
                .Build();

        public RabbitMqTests()
        {
        }

        public Task InitializeAsync()
        {
            return _rabbitMqContainer.StartAsync();
        }

        public Task DisposeAsync()
        {
            return _rabbitMqContainer.DisposeAsync().AsTask();
        }

        [Fact(DisplayName = "Can connect to docker RabbitMq")]
        public async Task Can_Connect_ToRabbit()
        {

            // Arrange
            var connectionData = new ConnectionData()
            {
                ConnectionString = _rabbitMqContainer.GetConnectionString(),
                Username = "guest",
                Password = "guest"
            };

            var provider = new RabbitMqConnector();


            // Act
            var connection = await provider.ConnectAsync(connectionData, false);

            // Assert
            connection.Should().NotBeNull();
            connection.Address.Should().NotBeNullOrWhiteSpace();
        }


        [Fact(DisplayName = "Can send message to docker RabbitMq")]
        public async Task Can_SendMessageTo_DockerRabbitMQ()
        {
            // Arrange
            var port = _rabbitMqContainer.GetMappedPublicPort(15672);

            var data = new ConnectionData()
            {
                ConnectionString = $"http://localhost:{port}",
                Username = "guest",
                Password = "guest"
            };

            var connector = new RabbitMqConnector();

            // Act
            var connection = await connector!.ConnectAsync(data, false);

            Func<Task> act = async () =>
            {
                await connection!.SendAsync(new EndpointDetails()
                {
                    Provider = "rabbitmq",
                    Address = _rabbitMqContainer.IpAddress,
                    Type = "queue",
                    Name = "integration-test"
                }, "{\"Red\": 0, \"Green\": 20, \"Blue\": 30}");
            };

            // Assert
            connection.Should().NotBeNull();
            connection!.Address.Should().ContainEquivalentOf(data.ConnectionString[..(data.ConnectionString.Length - 1)]);
            await act.Should().NotThrowAsync();
        }
    }
}