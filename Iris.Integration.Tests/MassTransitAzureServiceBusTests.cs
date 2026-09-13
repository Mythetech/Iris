using System;
using System.Threading.Tasks;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Azure;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Iris.Integration.Tests.Fixtures;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Iris.Integration.Tests
{
    // Top-level so MassTransit's MessageUrn resolves to
    // "urn:message:Iris.Integration.Tests:IrisAsbTestMessage".
    public record IrisAsbTestMessage(int Red, int Green, int Blue);

    /// <summary>
    /// The Service Bus counterpart to <see cref="MassTransitTests"/>: an Iris-wrapped envelope,
    /// sent through the Azure connector, consumed by a real MassTransit bus.
    ///
    /// The queue is seeded on the emulator before the bus starts, and the receive endpoint is
    /// told not to configure consume topology. Declaring entities is not Iris's job and is not
    /// what this test is for; what matters is that what Iris puts on the wire is routed and
    /// deserialized by MassTransit's own receive pipeline.
    /// </summary>
    [Collection("AzureServiceBus")]
    [Trait("Category", "Container")]
    public class MassTransitAzureServiceBusTests : IAsyncLifetime
    {
        private const string QueueName = "iris-mt-asb-test";

        private readonly AzureServiceBusContainerFixture _emulator;
        private readonly TestIrisConsumer _consumer = new();

        private IBusControl? _bus;

        public MassTransitAzureServiceBusTests(AzureServiceBusContainerFixture emulator)
        {
            _emulator = emulator;
        }

        public async Task InitializeAsync()
        {
            Environment.SetEnvironmentVariable("MT_TELEMETRY", "false");

            await _emulator.CreateQueue(QueueName);

            _bus = Bus.Factory.CreateUsingAzureServiceBus(cfg =>
            {
                cfg.Host(_emulator.ConnectionString);

                cfg.ReceiveEndpoint(QueueName, e =>
                {
                    e.ConfigureConsumeTopology = false;
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

        [Theory(DisplayName = "Iris MassTransit-wrapped message round-trips to a real MassTransit consumer on Azure Service Bus")]
        // An Azure Service Bus entity name, which is how a discovered endpoint is spelled.
        [InlineData("Iris.Integration.Tests/IrisAsbTestMessage")]
        // A RabbitMQ exchange name, which is already the urn spelling.
        [InlineData("Iris.Integration.Tests:IrisAsbTestMessage")]
        // A .NET fully qualified name, which is what the type picker produces.
        [InlineData("Iris.Integration.Tests.IrisAsbTestMessage")]
        public async Task Can_Consume_MassTransit_Message(string typeName)
        {
            var request = MessageRequest.Create(
                messageType: QueueName,
                json: "{\"Red\":1,\"Green\":2,\"Blue\":3}",
                generateIrisHeaders: false,
                messageFullyQualifiedName: typeName,
                framework: "MassTransit");

            request.WrapMessage(new MassTransitAdapter());

            var connection = CreateConnection();

            await connection.SendAsync(
                new EndpointDetails
                {
                    Provider = "AzureServiceBus",
                    Address = _emulator.ConnectionString,
                    Name = QueueName,
                    Type = "Queue",
                },
                request);

            var completed = await Task.WhenAny(
                _consumer.Received.Task,
                Task.Delay(TimeSpan.FromSeconds(30)));

            completed.Should().BeSameAs(
                _consumer.Received.Task,
                "MassTransit should consume the Iris-wrapped envelope within 30s; a timeout means the adapter produced a wire format MassTransit cannot route or deserialize");

            var context = await _consumer.Received.Task;
            context.Message.Red.Should().Be(1);
            context.Message.Green.Should().Be(2);
            context.Message.Blue.Should().Be(3);
            context.MessageId.Should().NotBeNull();
            context.SourceAddress.Should().Be(new Uri("loopback://localhost/iris"));
            context.RequestId.Should().BeNull();
            context.InitiatorId.Should().BeNull();
        }

        private AzureServiceBusConnection CreateConnection()
        {
            var metadata = new ConnectionMetadata
            {
                Connector = new AzureConnector(NullLoggerFactory.Instance),
                Address = _emulator.ConnectionString,
            };

            return new AzureServiceBusConnection(
                metadata,
                new ServiceBusAdministrationClient(_emulator.ConnectionString),
                new ServiceBusClient(_emulator.ConnectionString));
        }

        private sealed class TestIrisConsumer : IConsumer<IrisAsbTestMessage>
        {
            public TaskCompletionSource<ConsumeContext<IrisAsbTestMessage>> Received { get; }
                = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task Consume(ConsumeContext<IrisAsbTestMessage> context)
            {
                Received.TrySetResult(context);
                return Task.CompletedTask;
            }
        }
    }
}
