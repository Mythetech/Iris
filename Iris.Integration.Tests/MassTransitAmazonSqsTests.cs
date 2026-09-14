using System;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Amazon;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Iris.Integration.Tests.Fixtures;
using MassTransit;

namespace Iris.Integration.Tests
{
    // Top-level so MassTransit's MessageUrn resolves to
    // "urn:message:Iris.Integration.Tests:IrisSqsTestMessage".
    public record IrisSqsTestMessage(int Red, int Green, int Blue);

    /// <summary>
    /// The Amazon SQS counterpart to <see cref="MassTransitTests"/> and
    /// <see cref="MassTransitAzureServiceBusTests"/>: an Iris-wrapped envelope, sent through the
    /// Amazon connector, consumed by a real MassTransit bus.
    ///
    /// Unlike the Service Bus test, nothing is pre-seeded here: LocalEmu serves SNS as well as
    /// SQS, so MassTransit stands up its own topology, which is a topic per message type plus a
    /// subscription feeding the endpoint queue.
    /// </summary>
    [Collection("LocalEmu")]
    [Trait("Category", "Container")]
    public class MassTransitAmazonSqsTests
    {
        private readonly LocalEmuContainerFixture _emulator;

        public MassTransitAmazonSqsTests(LocalEmuContainerFixture emulator)
        {
            _emulator = emulator;
        }

        [Theory(DisplayName = "Iris MassTransit-wrapped message round-trips to a real MassTransit consumer on Amazon SQS")]
        // An Azure Service Bus entity name, which is how a discovered endpoint is spelled.
        [InlineData("Iris.Integration.Tests/IrisSqsTestMessage", "slash")]
        // A RabbitMQ exchange name, which is already the urn spelling.
        [InlineData("Iris.Integration.Tests:IrisSqsTestMessage", "colon")]
        // A .NET fully qualified name, which is what the type picker produces.
        [InlineData("Iris.Integration.Tests.IrisSqsTestMessage", "dot")]
        public async Task Can_Consume_MassTransit_Message(string typeName, string queueSuffix)
        {
            Environment.SetEnvironmentVariable("MT_TELEMETRY", "false");

            // Each case gets its own queue and its own bus. Sharing one queue across the theory
            // let one case's bus take a sibling's message and the sibling then timed out waiting
            // for a message that had already been consumed.
            var queueName = $"iris-mt-sqs-{queueSuffix}";
            var consumer = new TestIrisConsumer();

            var bus = Bus.Factory.CreateUsingAmazonSqs(cfg =>
            {
                cfg.Host("us-east-1", h =>
                {
                    h.AccessKey("test");
                    h.SecretKey("test");
                    h.Config(new AmazonSQSConfig { ServiceURL = _emulator.ServiceUrl });
                    h.Config(new AmazonSimpleNotificationServiceConfig { ServiceURL = _emulator.ServiceUrl });
                });

                cfg.ReceiveEndpoint(queueName, e => e.Consumer(() => consumer));
            });

            await bus.StartAsync(TestContext.Current.CancellationToken);

            try
            {
                var request = MessageRequest.Create(
                    messageType: queueName,
                    json: "{\"Red\":1,\"Green\":2,\"Blue\":3}",
                    generateIrisHeaders: false,
                    messageFullyQualifiedName: typeName,
                    framework: "MassTransit");

                request.WrapMessage(new MassTransitAdapter());

                using var client = CreateSqsClient();
                var connection = CreateConnection(client);

                await connection.SendAsync(
                    new EndpointDetails
                    {
                        Provider = "Amazon",
                        Address = _emulator.ServiceUrl,
                        Type = "Queue",
                        Name = queueName,
                    },
                    request);

                var context = await Eventually.CompletesAsync(
                    consumer.Received.Task,
                    TimeSpan.FromSeconds(30),
                    "MassTransit consumes the Iris-wrapped envelope; a timeout means the adapter produced a wire format MassTransit cannot route or deserialize",
                    TestContext.Current.CancellationToken);
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
                await bus.StopAsync(TestContext.Current.CancellationToken);
            }
        }

        private AmazonSQSClient CreateSqsClient() => new(
            new BasicAWSCredentials("test", "test"),
            new AmazonSQSConfig
            {
                ServiceURL = _emulator.ServiceUrl,
                AuthenticationRegion = "us-east-1",
            });

        private AmazonSimpleQueueServiceConnection CreateConnection(AmazonSQSClient client) => new(
            new ConnectionMetadata
            {
                Connector = new AmazonWebServicesConnector(),
                Address = _emulator.ServiceUrl,
            },
            client);

        private sealed class TestIrisConsumer : IConsumer<IrisSqsTestMessage>
        {
            public TaskCompletionSource<ConsumeContext<IrisSqsTestMessage>> Received { get; }
                = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task Consume(ConsumeContext<IrisSqsTestMessage> context)
            {
                Received.TrySetResult(context);
                return Task.CompletedTask;
            }
        }
    }
}
