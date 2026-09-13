using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Azure;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Iris.Integration.Tests.Fixtures;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;

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
    public class MassTransitAzureServiceBusTests
    {
        private readonly AzureServiceBusContainerFixture _emulator;

        public MassTransitAzureServiceBusTests(AzureServiceBusContainerFixture emulator)
        {
            _emulator = emulator;
        }

        [Theory(DisplayName = "Iris MassTransit-wrapped message round-trips to a real MassTransit consumer on Azure Service Bus")]
        // An Azure Service Bus entity name, which is how a discovered endpoint is spelled.
        [InlineData("Iris.Integration.Tests/IrisAsbTestMessage", "slash")]
        // A RabbitMQ exchange name, which is already the urn spelling.
        [InlineData("Iris.Integration.Tests:IrisAsbTestMessage", "colon")]
        // A .NET fully qualified name, which is what the type picker produces.
        [InlineData("Iris.Integration.Tests.IrisAsbTestMessage", "dot")]
        public async Task Can_Consume_MassTransit_Message(string typeName, string queueSuffix)
        {
            Environment.SetEnvironmentVariable("MT_TELEMETRY", "false");

            // Each case gets its own queue and its own bus, so one case's bus can never take a
            // sibling's message and leave the sibling waiting for one already consumed.
            var queueName = $"iris-mt-asb-{queueSuffix}";
            var consumer = new TestIrisConsumer();

            await _emulator.CreateQueue(queueName);

            var bus = Bus.Factory.CreateUsingAzureServiceBus(cfg =>
            {
                cfg.Host(_emulator.ConnectionString);

                cfg.ReceiveEndpoint(queueName, e =>
                {
                    e.ConfigureConsumeTopology = false;
                    e.Consumer(() => consumer);
                });
            });

            await bus.StartAsync();

            try
            {
                var request = MessageRequest.Create(
                    messageType: queueName,
                    json: "{\"Red\":1,\"Green\":2,\"Blue\":3}",
                    generateIrisHeaders: false,
                    messageFullyQualifiedName: typeName,
                    framework: "MassTransit");

                request.WrapMessage(new MassTransitAdapter());

                await CreateConnection().SendAsync(
                    new EndpointDetails
                    {
                        Provider = "AzureServiceBus",
                        Address = _emulator.ConnectionString,
                        Name = queueName,
                        Type = "Queue",
                    },
                    request);

                var completed = await Task.WhenAny(
                    consumer.Received.Task,
                    Task.Delay(TimeSpan.FromSeconds(30)));

                completed.Should().BeSameAs(
                    consumer.Received.Task,
                    "MassTransit should consume the Iris-wrapped envelope within 30s; a timeout means the adapter produced a wire format MassTransit cannot route or deserialize");

                var context = await consumer.Received.Task;
                context.Message.Red.Should().Be(1);
                context.Message.Green.Should().Be(2);
                context.Message.Blue.Should().Be(3);
                context.MessageId.Should().NotBeNull();
                context.SourceAddress.Should().Be(new Uri("loopback://localhost/iris"));
                context.RequestId.Should().BeNull();
                context.InitiatorId.Should().BeNull();
            }
            finally
            {
                await bus.StopAsync();
            }
        }

        private AzureServiceBusConnection CreateConnection() => new(
            new ConnectionMetadata
            {
                Connector = new AzureConnector(NullLoggerFactory.Instance),
                Address = _emulator.ConnectionString,
            },
            new ServiceBusAdministrationClient(_emulator.ConnectionString),
            new ServiceBusClient(_emulator.ConnectionString));

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
