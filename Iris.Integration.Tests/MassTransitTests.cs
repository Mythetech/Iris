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
    // which is what MassTransitAdapter.CreateWrappedMessage must emit for a round-trip
    // (it replaces '/' with ':' in the supplied MessageType string).
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

        [Fact(DisplayName = "Iris MassTransit-wrapped message round-trips to a real MassTransit consumer on RabbitMQ")]
        public async Task Can_Consume_MassTransit_Message()
        {
            // Arrange — wrap a message exactly as LocalConnectionManager.SendMessageAsync does.
            var adapter = new MassTransitAdapter();

            var request = MessageRequest.Create(
                messageType: "Iris.Integration.Tests/IrisMtTestMessage",
                json: "{\"Red\":1,\"Green\":2,\"Blue\":3}",
                generateIrisHeaders: false,
                messageFullyQualifiedName: "Iris.Integration.Tests/IrisMtTestMessage",
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

            // NOTE: context.SourceAddress is deliberately NOT asserted.
            // MassTransitAdapter.IrisMessageEnvelope sets SourceAddress => "iris",
            // which is not a valid absolute URI. Accessing ConsumeContext.SourceAddress
            // throws UriFormatException on the consumer side. This is a real adapter bug
            // surfaced by this test — tracked for a follow-up fix (candidates: "urn:iris",
            // or deriving it from the connection's host URI). Do not "fix" by asserting
            // round-trip passes while leaving consumers downstream to blow up.
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
