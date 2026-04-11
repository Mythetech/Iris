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

        [Fact(DisplayName = "RabbitMqConnection implements all four read interfaces")]
        public async Task RabbitMqConnection_implements_reader_interfaces()
        {
            var connection = await CreateAndSeedAsync("iris-iface-probe", 0);

            connection.Should().BeAssignableTo<IMessagePeeker>();
            connection.Should().BeAssignableTo<IMessageReceiver>();
            connection.Should().BeAssignableTo<IDeadLetterPeeker>();
            connection.Should().BeAssignableTo<IDeadLetterReceiver>();
        }

        /// <summary>
        /// Declare a DLX exchange + DLQ target queue + source queue whose
        /// <c>x-dead-letter-exchange</c> points at the DLX and whose
        /// <c>x-message-ttl</c> is short enough that every sent message
        /// auto-dead-letters almost immediately. Publishes <paramref name="count"/>
        /// messages to the source and waits for the TTL to flush them into the DLQ.
        /// </summary>
        private async Task<IConnection> CreateDlxTopologyAndSeedAsync(
            string sourceQueue, string dlxExchange, string dlqQueue, int count, int ttlMs = 100)
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

            var mgmt = new EasyNetQ.Management.Client.ManagementClient(
                new Uri($"http://localhost:{port}"), "guest", "guest");
            var vhost = await mgmt.GetVhostAsync("/");

            // 1) DLX — a fanout exchange is fine; we only bind one DLQ to it.
            await mgmt.CreateExchangeAsync(vhost, dlxExchange,
                new EasyNetQ.Management.Client.Model.ExchangeInfo(
                    Type: "fanout", AutoDelete: false, Durable: true),
                CancellationToken.None);

            // 2) DLQ target — plain queue, no args.
            await mgmt.CreateQueueAsync(vhost, dlqQueue,
                new EasyNetQ.Management.Client.Model.QueueInfo(
                    AutoDelete: false, Durable: true, Arguments: null),
                CancellationToken.None);

            // 3) Bind DLQ to DLX — fanout ignores routing key.
            await mgmt.CreateQueueBindingAsync(vhost, dlxExchange, dlqQueue,
                new EasyNetQ.Management.Client.Model.BindingInfo(RoutingKey: ""),
                CancellationToken.None);

            // 4) Source queue with DLX pointer + TTL so messages auto-dead-letter.
            await mgmt.CreateQueueAsync(vhost, sourceQueue,
                new EasyNetQ.Management.Client.Model.QueueInfo(
                    AutoDelete: false,
                    Durable: true,
                    Arguments: new Dictionary<string, object?>
                    {
                        ["x-dead-letter-exchange"] = dlxExchange,
                        ["x-message-ttl"] = ttlMs,
                    }),
                CancellationToken.None);

            for (var i = 0; i < count; i++)
            {
                await connection!.SendAsync(new EndpointDetails
                {
                    Provider = "rabbitmq",
                    Address = _rabbitMqContainer.IpAddress,
                    Type = "queue",
                    Name = sourceQueue
                }, $"{{\"dlq-index\":{i}}}");
            }

            // Give TTL enough time to fire and messages to land on DLQ.
            await Task.Delay(ttlMs * 5);
            return connection!;
        }

        [Fact(DisplayName = "PeekDeadLetter returns dead-lettered messages non-destructively")]
        public async Task PeekDeadLetter_returns_dead_lettered_messages()
        {
            const string source = "iris-dlq-peek-source";
            const string dlx = "iris-dlq-peek-dlx";
            const string dlq = "iris-dlq-peek-target";
            var connection = await CreateDlxTopologyAndSeedAsync(source, dlx, dlq, count: 3);
            var peeker = (IDeadLetterPeeker)connection;

            var endpoint = new EndpointDetails
            {
                Provider = "rabbitmq",
                Address = connection.Address,
                Type = "Queue",
                Name = source
            };

            var first = await peeker.PeekDeadLetterAsync(endpoint, 10);
            var second = await peeker.PeekDeadLetterAsync(endpoint, 10);

            first.Should().HaveCount(3);
            second.Should().HaveCount(3);
            first.Select(m => m.Body).Should().BeEquivalentTo(second.Select(m => m.Body));
            first.Should().OnlyContain(m => m.Source == ReadSource.DeadLetter);
        }

        [Fact(DisplayName = "ReceiveDeadLetter removes dead-lettered messages from the DLQ")]
        public async Task ReceiveDeadLetter_removes_dead_lettered_messages()
        {
            const string source = "iris-dlq-receive-source";
            const string dlx = "iris-dlq-receive-dlx";
            const string dlq = "iris-dlq-receive-target";
            var connection = await CreateDlxTopologyAndSeedAsync(source, dlx, dlq, count: 3);
            var peeker = (IDeadLetterPeeker)connection;
            var receiver = (IDeadLetterReceiver)connection;

            var endpoint = new EndpointDetails
            {
                Provider = "rabbitmq",
                Address = connection.Address,
                Type = "Queue",
                Name = source
            };

            var received = await receiver.ReceiveDeadLetterAsync(endpoint, 10);
            var afterReceive = await peeker.PeekDeadLetterAsync(endpoint, 10);

            received.Should().HaveCount(3);
            received.Should().OnlyContain(m => m.Source == ReadSource.DeadLetter);
            afterReceive.Should().BeEmpty();
        }

        [Fact(DisplayName = "ReceiveDeadLetter returns empty when no DLX configured")]
        public async Task ReceiveDeadLetter_returns_empty_when_no_DLX_configured()
        {
            // CreateAndSeedAsync declares a plain queue with null Arguments — no
            // x-dead-letter-exchange. The honest answer to "what's in the DLQ?" is
            // "nothing, because there isn't one." No exception.
            const string queue = "iris-dlq-none";
            var connection = await CreateAndSeedAsync(queue, 2);
            var receiver = (IDeadLetterReceiver)connection;

            var endpoint = new EndpointDetails
            {
                Provider = "rabbitmq",
                Address = connection.Address,
                Type = "Queue",
                Name = queue
            };

            var received = await receiver.ReceiveDeadLetterAsync(endpoint, 10);

            received.Should().BeEmpty();
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