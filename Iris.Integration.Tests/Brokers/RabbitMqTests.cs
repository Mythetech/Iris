using System;
using System.Collections.Immutable;
using EasyNetQ.Management.Client;
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
                .WithPortBinding(15672, true)
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

        private async Task<IConnection> CreateAndSeedAsync(string queueName, int count)
        {
            var port = _rabbitMqContainer.GetMappedPublicPort(15672);
            var data = new ConnectionData
            {
                ConnectionString = $"http://localhost:{port}",
                Username = "guest",
                Password = "guest"
            };
            var connector = new RabbitMqConnector();
            var connection = await connector.ConnectAsync(data, false);

            // Ensure queue exists by declaring via management client used under the hood.
            // Easiest path: publish a message — the RabbitMQ management publish-to-default-exchange
            // requires the queue to exist already, so we create it first by publishing through
            // SendAsync which routes via amq.default and relies on an existing queue.
            // To create the queue idempotently we publish count messages AFTER declaring it via
            // a direct management API call.
            var mgmt = new EasyNetQ.Management.Client.ManagementClient(
                new Uri($"http://localhost:{port}"), "guest", "guest");
            var vhost = await mgmt.GetVhostAsync("/");
            await mgmt.CreateQueueAsync(vhost, queueName,
                new EasyNetQ.Management.Client.Model.QueueInfo(AutoDelete: false, Durable: true, Arguments: null!),
                CancellationToken.None);

            for (var i = 0; i < count; i++)
            {
                await connection!.SendAsync(new EndpointDetails
                {
                    Provider = "rabbitmq",
                    Address = _rabbitMqContainer.IpAddress,
                    Type = "queue",
                    Name = queueName
                }, $"{{\"index\":{i}}}");
            }

            return connection!;
        }

        [Fact(DisplayName = "RabbitMqConnection implements IMessagePeeker and IMessageReceiver")]
        public async Task RabbitMqConnection_implements_reader_interfaces()
        {
            var connection = await CreateAndSeedAsync("iris-iface-probe", 0);

            connection.Should().BeAssignableTo<IMessagePeeker>();
            connection.Should().BeAssignableTo<IMessageReceiver>();
            connection.Should().NotBeAssignableTo<IDeadLetterPeeker>();
            connection.Should().NotBeAssignableTo<IDeadLetterReceiver>();
        }

        [Fact(DisplayName = "Peek returns sent messages without removing them")]
        public async Task Peek_is_non_destructive()
        {
            const string queue = "iris-peek-test";
            var connection = await CreateAndSeedAsync(queue, 3);
            var peeker = (IMessagePeeker)connection;

            var endpoint = new EndpointDetails
            {
                Provider = "rabbitmq",
                Address = connection.Address,
                Type = "Queue",
                Name = queue
            };

            var first = await peeker.PeekAsync(endpoint, 3);
            var second = await peeker.PeekAsync(endpoint, 3);

            first.Should().HaveCount(3);
            second.Should().HaveCount(3);
            first.Select(m => m.Body).Should().BeEquivalentTo(second.Select(m => m.Body));
        }

        [Fact(DisplayName = "Receive removes messages from the queue")]
        public async Task Receive_is_destructive()
        {
            const string queue = "iris-receive-test";
            var connection = await CreateAndSeedAsync(queue, 3);
            var peeker = (IMessagePeeker)connection;
            var receiver = (IMessageReceiver)connection;

            var endpoint = new EndpointDetails
            {
                Provider = "rabbitmq",
                Address = connection.Address,
                Type = "Queue",
                Name = queue
            };

            var received = await receiver.ReceiveAsync(endpoint, 3);
            var afterPeek = await peeker.PeekAsync(endpoint, 3);

            received.Should().HaveCount(3);
            afterPeek.Should().BeEmpty();
        }

        [Fact(DisplayName = "Peeked messages carry provider and native metadata")]
        public async Task Peek_maps_metadata()
        {
            const string queue = "iris-metadata-test";
            var connection = await CreateAndSeedAsync(queue, 1);
            var peeker = (IMessagePeeker)connection;

            var endpoint = new EndpointDetails
            {
                Provider = "rabbitmq",
                Address = connection.Address,
                Type = "Queue",
                Name = queue
            };

            var msgs = await peeker.PeekAsync(endpoint, 1);

            msgs.Should().HaveCount(1);
            var msg = msgs[0];
            msg.Provider.Should().Be("RabbitMQ");
            msg.Body.Should().Contain("\"index\":0");
            msg.Native.Should().NotBeNull();
            msg.Native!.RoutingKey.Should().Be(queue);
        }
    }
}