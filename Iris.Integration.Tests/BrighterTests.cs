using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Iris.Brokers.RabbitMQ;
using Iris.Integration.Tests.Fixtures;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Testcontainers.RabbitMq;

namespace Iris.Integration.Tests
{
    [Collection("RabbitMQ")]
    [Trait("Category", "Container")]
    public class BrighterTests : IAsyncLifetime
    {
        private const string QueueName = "iris-brighter-test";
        private const string TypeName = "MyApp.Messages.OrderPlaced";
        private const string Json = "{\"orderId\":42}";

        private readonly RabbitMqContainer _rabbitMqContainer;
        private RmqMessageConsumer? _consumer;

        public BrighterTests(RabbitMqContainerFixture fixture)
        {
            _rabbitMqContainer = fixture.Container;
        }

        public async ValueTask InitializeAsync()
        {
            var amqpPort = _rabbitMqContainer.GetMappedPublicPort(5672);

            var connection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri($"amqp://guest:guest@localhost:{amqpPort}/%2f")),
                Exchange = new Exchange("iris.brighter.exchange"),
            };

            _consumer = new RmqMessageConsumer(connection, new ChannelName(QueueName), new RoutingKey(QueueName), isDurable: false);

            // The consumer declares and binds its queue on first use; force that before Iris publishes.
            await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(100));
        }

        public ValueTask DisposeAsync()
        {
            _consumer?.Dispose();
            return ValueTask.CompletedTask;
        }

        [Fact(DisplayName = "Iris Brighter-wrapped message is parsed by a real Brighter RMQ consumer")]
        public async Task Can_Consume_Brighter_Message()
        {
            var request = MessageRequest.Create(
                messageType: "OrderPlaced",
                json: Json,
                generateIrisHeaders: false,
                messageFullyQualifiedName: TypeName,
                framework: "Brighter",
                properties: new Dictionary<string, string>
                {
                    [BrighterAdapter.TopicKey] = QueueName,
                    [BrighterAdapter.MessageKindKey] = "MT_COMMAND",
                });

            request.WrapMessage(new BrighterAdapter());

            var managementPort = _rabbitMqContainer.GetMappedPublicPort(15672);
            var connection = await new RabbitMqConnector().ConnectAsync(new ConnectionData
            {
                ConnectionString = $"http://localhost:{managementPort}",
                Username = "guest",
                Password = "guest",
            }, false);
            connection.Should().NotBeNull();

            await connection!.SendAsync(new EndpointDetails
            {
                Provider = "rabbitmq",
                Address = $"http://localhost:{managementPort}",
                Name = QueueName,
                Type = "Queue",
            }, request);

            Message? received = null;
            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while (received is null && DateTimeOffset.UtcNow < deadline)
            {
                var batch = await _consumer!.ReceiveAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
                received = batch.FirstOrDefault(m => m.Header.MessageType != MessageType.MT_NONE);
            }

            received.Should().NotBeNull("Brighter should parse the message within 30s; MT_NONE means RmqMessageCreator rejected the headers");
            received!.Header.MessageType.Should().Be(MessageType.MT_COMMAND);
            received.Header.Topic.Value.Should().Be(QueueName);
            received.Header.Type.Value.Should().Be(TypeName);
            received.Header.ContentType.MediaType.Should().Be("application/json");
            received.Body.Value.Should().Be(Json);

            await _consumer!.AcknowledgeAsync(received, TestContext.Current.CancellationToken);
        }
    }
}
