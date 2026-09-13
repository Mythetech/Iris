using System;
using System.Threading.Tasks;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Iris.Brokers.RabbitMQ;
using Iris.Integration.Tests.Fixtures;
using MassTransit;
using Testcontainers.RabbitMq;

namespace Iris.Integration.Tests
{
    // Top-level so MassTransit's MessageUrn resolves to
    // "urn:message:Iris.Integration.Tests:IrisMtTestMessage",
    // which is what MassTransitAdapter.CreateWrappedMessage must emit for a round-trip.
    // The adapter has to reach that one urn from three different spellings of the same type,
    // which is what the theory below covers.
    public record IrisMtTestMessage(int Red, int Green, int Blue);

    [Collection("RabbitMQ")]
    [Trait("Category", "Container")]
    public class MassTransitTests : IAsyncLifetime
    {
        private const string QueueName = "iris-mt-test";

        private readonly RabbitMqContainer _rabbitMqContainer;

        private IBusControl? _bus;
        private readonly TestIrisConsumer _consumer = new();

        public MassTransitTests(RabbitMqContainerFixture fixture)
        {
            _rabbitMqContainer = fixture.Container;
        }

        public async Task InitializeAsync()
        {
            Environment.SetEnvironmentVariable("MT_TELEMETRY", "false");

            var amqpPort = _rabbitMqContainer.GetMappedPublicPort(5672);

            _bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
            {
                cfg.Host(new Uri($"rabbitmq://localhost:{amqpPort}/"), h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });

                cfg.ReceiveEndpoint(QueueName, e =>
                {
                    e.Consumer(() => _consumer);
                });
            });

            await _bus.StartAsync();
        }

        public async Task DisposeAsync()
        {
            if (_bus is not null)
            {
                await _bus.StopAsync();
            }
        }

        [Theory(DisplayName = "Iris MassTransit-wrapped message round-trips to a real MassTransit consumer on RabbitMQ")]
        // An Azure Service Bus entity name, which is how a discovered endpoint is spelled.
        [InlineData("Iris.Integration.Tests/IrisMtTestMessage")]
        // A RabbitMQ exchange name, which is already the urn spelling.
        [InlineData("Iris.Integration.Tests:IrisMtTestMessage")]
        // A .NET fully qualified name, which is what the type picker and the Type name field's
        // help text produce. This is the spelling that silently stopped routing.
        [InlineData("Iris.Integration.Tests.IrisMtTestMessage")]
        public async Task Can_Consume_MassTransit_Message(string typeName)
        {
            // Arrange — wrap a message exactly as LocalConnectionManager.SendMessageAsync does.
            var adapter = new MassTransitAdapter();

            var request = MessageRequest.Create(
                messageType: "Iris.Integration.Tests/IrisMtTestMessage",
                json: "{\"Red\":1,\"Green\":2,\"Blue\":3}",
                generateIrisHeaders: false,
                messageFullyQualifiedName: typeName,
                framework: "MassTransit");

            request.WrapMessage(adapter);

            var managementPort = _rabbitMqContainer.GetMappedPublicPort(15672);
            var connectionData = new ConnectionData
            {
                ConnectionString = $"http://localhost:{managementPort}",
                Username = "guest",
                Password = "guest",
            };

            var connector = new RabbitMqConnector();
            var connection = await connector.ConnectAsync(connectionData, false);
            connection.Should().NotBeNull("RabbitMqConnector must connect to the management API");

            // Act — publish the wrapped envelope to the queue MassTransit is consuming from.
            await connection!.SendAsync(
                new EndpointDetails
                {
                    Provider = "rabbitmq",
                    Address = $"http://localhost:{managementPort}",
                    Name = QueueName,
                    Type = "Queue",
                },
                request);

            // Assert — the MassTransit consumer must deserialize and receive the payload.
            var completed = await Task.WhenAny(
                _consumer.Received.Task,
                Task.Delay(TimeSpan.FromSeconds(30)));

            completed.Should().BeSameAs(
                _consumer.Received.Task,
                "MassTransit should consume the Iris-wrapped envelope within 30s — a timeout means the adapter produced a wire format MassTransit can't route or deserialize");

            var context = await _consumer.Received.Task;
            context.Message.Red.Should().Be(1);
            context.Message.Green.Should().Be(2);
            context.Message.Blue.Should().Be(3);
            context.MessageId.Should().NotBeNull();

            // SourceAddress used to be the bare string "iris", so reading this property threw
            // UriFormatException on the consumer side. Asserting it keeps the address absolute.
            context.SourceAddress.Should().Be(new Uri("loopback://localhost/iris"));

            // A non-null RequestId makes a consumer treat the send as a request awaiting a
            // response, which is not what a user typing a message into Iris asked for.
            context.RequestId.Should().BeNull();
            context.InitiatorId.Should().BeNull();
        }

        private sealed class TestIrisConsumer : IConsumer<IrisMtTestMessage>
        {
            public TaskCompletionSource<ConsumeContext<IrisMtTestMessage>> Received { get; }
                = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task Consume(ConsumeContext<IrisMtTestMessage> context)
            {
                Received.TrySetResult(context);
                return Task.CompletedTask;
            }
        }
    }
}
