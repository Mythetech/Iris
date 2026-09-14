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
    public class MassTransitTests
    {
        private readonly RabbitMqContainer _rabbitMqContainer;

        public MassTransitTests(RabbitMqContainerFixture fixture)
        {
            _rabbitMqContainer = fixture.Container;
        }

        [Theory(DisplayName = "Iris MassTransit-wrapped message round-trips to a real MassTransit consumer on RabbitMQ")]
        // An Azure Service Bus entity name, which is how a discovered endpoint is spelled.
        [InlineData("Iris.Integration.Tests/IrisMtTestMessage", "slash")]
        // A RabbitMQ exchange name, which is already the urn spelling.
        [InlineData("Iris.Integration.Tests:IrisMtTestMessage", "colon")]
        // A .NET fully qualified name, which is what the type picker and the Type name field's
        // help text produce. This is the spelling that silently stopped routing.
        [InlineData("Iris.Integration.Tests.IrisMtTestMessage", "dot")]
        public async Task Can_Consume_MassTransit_Message(string typeName, string queueSuffix)
        {
            Environment.SetEnvironmentVariable("MT_TELEMETRY", "false");

            // Each case gets its own queue and its own bus, so one case's bus can never take a
            // sibling's message and leave the sibling waiting for one already consumed.
            var queueName = $"iris-mt-{queueSuffix}";
            var consumer = new TestIrisConsumer();
            var amqpPort = _rabbitMqContainer.GetMappedPublicPort(5672);

            var bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
            {
                cfg.Host(new Uri($"rabbitmq://localhost:{amqpPort}/"), h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });

                cfg.ReceiveEndpoint(queueName, e => e.Consumer(() => consumer));
            });

            await bus.StartAsync(TestContext.Current.CancellationToken);

            try
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
                        Name = queueName,
                        Type = "Queue",
                    },
                    request);

                // Assert — the MassTransit consumer must deserialize and receive the payload.
                var context = await Eventually.CompletesAsync(
                    consumer.Received.Task,
                    TimeSpan.FromSeconds(30),
                    "MassTransit consumes the Iris-wrapped envelope; a timeout means the adapter produced a wire format MassTransit cannot route or deserialize",
                    TestContext.Current.CancellationToken);
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
            finally
            {
                await bus.StopAsync(TestContext.Current.CancellationToken);
            }
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
