using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Amazon;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Iris.Integration.Tests.Fixtures;

namespace Iris.Integration.Tests.Brokers
{
    /// <summary>
    /// Emulator-backed integration tests for <see cref="AmazonSimpleQueueServiceConnection"/>.
    /// Uses LocalEmu so the suite can run committable tests without real AWS credentials, and
    /// so there is one AWS emulator rather than one per service.
    /// </summary>
    [Collection("LocalEmu")]
    [Trait("Category", TestCategories.Container)]
    public class AmazonSqsContainerTests
    {
        private readonly LocalEmuContainerFixture _emulator;

        public AmazonSqsContainerTests(LocalEmuContainerFixture emulator)
        {
            _emulator = emulator;
        }

        private AmazonSQSClient CreateSqsClient()
        {
            return new AmazonSQSClient(
                new BasicAWSCredentials("test", "test"),
                new AmazonSQSConfig
                {
                    ServiceURL = _emulator.ServiceUrl,
                    AuthenticationRegion = "us-east-1",
                });
        }

        private AmazonSimpleQueueServiceConnection CreateConnection(AmazonSQSClient client)
        {
            var connector = new AmazonWebServicesConnector();
            var metadata = new ConnectionMetadata
            {
                Connector = connector,
                Address = _emulator.ServiceUrl,
            };
            return new AmazonSimpleQueueServiceConnection(metadata, client);
        }

        private static EndpointDetails Endpoint(string queueName) => new()
        {
            Provider = "Amazon",
            Address = "localemu",
            Type = "Queue",
            Name = queueName,
        };

        // No Timeout: this test awaits nothing, so there is nothing for one to interrupt.
        [Fact(DisplayName = "AmazonSqsConnection implements receive + dlq receive only")]
        public Task Connection_implements_expected_interfaces()
        {
            using var client = CreateSqsClient();
            var connection = CreateConnection(client);

            connection.Should().BeAssignableTo<IMessageReceiver>();
            connection.Should().BeAssignableTo<IDeadLetterReceiver>();
            connection.Should().NotBeAssignableTo<IMessagePeeker>();
            connection.Should().NotBeAssignableTo<IDeadLetterPeeker>();
            return Task.CompletedTask;
        }

        [Fact(DisplayName = "Receive consumes messages and exposes message attributes", Timeout = 120000)]
        public async Task Receive_consumes_messages_with_attributes()
        {
            using var client = CreateSqsClient();
            var connection = CreateConnection(client);
            var receiver = (IMessageReceiver)connection;

            var queueName = "iris-sqs-receive-test";
            await client.CreateQueueAsync(queueName, TestContext.Current.CancellationToken);

            await client.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = (await client.GetQueueUrlAsync(queueName, TestContext.Current.CancellationToken)).QueueUrl,
                MessageBody = "{\"i\":1}",
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["customAttr"] = new() { DataType = "String", StringValue = "hello" },
                },
            }, TestContext.Current.CancellationToken);

            // Polled rather than slept on. An empty receive consumes nothing, so retrying is
            // free, and the send-to-visible latency is the emulator's business, not a number
            // this test should be guessing.
            var msgs = await Eventually.NonEmptyAsync(
                _ => receiver.ReceiveAsync(Endpoint(queueName), 10),
                "the sent message becomes visible on the queue",
                TestContext.Current.CancellationToken);

            msgs.Should().NotBeEmpty();
            var msg = msgs[0];
            msg.Body.Should().Be("{\"i\":1}");
            msg.Provider.Should().Be("AmazonSQS");
            msg.Properties.Should().ContainKey("customAttr");
            msg.Properties["customAttr"].Should().Be("hello");
            msg.Native!.ReceiptHandle.Should().NotBeNullOrWhiteSpace();
        }

        [Fact(DisplayName = "ReceiveDeadLetter returns empty when no RedrivePolicy", Timeout = 120000)]
        public async Task ReceiveDeadLetter_returns_empty_when_no_redrive_policy()
        {
            using var client = CreateSqsClient();
            var connection = CreateConnection(client);
            var dlqReceiver = (IDeadLetterReceiver)connection;

            var queueName = "iris-sqs-no-dlq-test";
            await client.CreateQueueAsync(queueName, TestContext.Current.CancellationToken);

            var dlqMsgs = await dlqReceiver.ReceiveDeadLetterAsync(Endpoint(queueName), 10, TestContext.Current.CancellationToken);

            dlqMsgs.Should().BeEmpty();
        }

        [Fact(DisplayName = "ReceiveDeadLetter reads from RedrivePolicy target queue", Timeout = 120000)]
        public async Task ReceiveDeadLetter_reads_from_redrive_target()
        {
            using var client = CreateSqsClient();
            var connection = CreateConnection(client);
            var receiver = (IMessageReceiver)connection;
            var dlqReceiver = (IDeadLetterReceiver)connection;

            var dlqName = "iris-sqs-dlq";
            var mainName = "iris-sqs-main";

            var dlqUrlResp = await client.CreateQueueAsync(dlqName, TestContext.Current.CancellationToken);
            var dlqAttrs = await client.GetQueueAttributesAsync(new GetQueueAttributesRequest
            {
                QueueUrl = dlqUrlResp.QueueUrl,
                AttributeNames = new List<string> { "QueueArn" },
            }, TestContext.Current.CancellationToken);
            var dlqArn = dlqAttrs.Attributes["QueueArn"];

            var mainUrlResp = await client.CreateQueueAsync(new CreateQueueRequest
            {
                QueueName = mainName,
                Attributes = new Dictionary<string, string>
                {
                    ["RedrivePolicy"] =
                        $"{{\"deadLetterTargetArn\":\"{dlqArn}\",\"maxReceiveCount\":1}}",
                },
            }, TestContext.Current.CancellationToken);
            var mainUrl = mainUrlResp.QueueUrl;

            // Send a message to the main queue, then drive its delivery count past
            // maxReceiveCount=1 so the broker moves it to the DLQ.
            await client.SendMessageAsync(mainUrl, "{\"will\":\"dead-letter\"}", TestContext.Current.CancellationToken);

            // Step 1: take delivery once with a one second visibility timeout, and do not
            // delete it, so the timer lapses and the broker re-attempts delivery.
            var firstAttempt = await Eventually.Async(
                _ => client.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = mainUrl,
                    MaxNumberOfMessages = 1,
                    VisibilityTimeout = 1,
                    WaitTimeSeconds = 1,
                }),
                response => response.Messages is { Count: > 0 },
                "the message is delivered from the main queue once",
                TestContext.Current.CancellationToken);

            firstAttempt.Messages.Should().NotBeNull().And.NotBeEmpty();

            // Step 2: keep re-receiving until the DLQ has it. Each attempt that finds the
            // message visible again counts as another delivery and is what actually trips
            // the redrive policy, so the poll drives the broker rather than waiting on it.
            //
            // This replaces a sleep sized to the visibility timeout followed by a single
            // receive whose own comment admitted it did not know what it would return.
            var dlqMessages = await Eventually.NonEmptyAsync(
                async _ =>
                {
                    await client.ReceiveMessageAsync(new ReceiveMessageRequest
                    {
                        QueueUrl = mainUrl,
                        MaxNumberOfMessages = 1,
                        VisibilityTimeout = 1,
                        WaitTimeSeconds = 1,
                    });

                    return await dlqReceiver.ReceiveDeadLetterAsync(Endpoint(mainName), 10);
                },
                "the message is redriven to the dead letter queue",
                TestContext.Current.CancellationToken,
                interval: TimeSpan.FromMilliseconds(500));

            dlqMessages.Should().NotBeEmpty(
                "the message should have been redriven to the DLQ after exceeding maxReceiveCount=1");
            dlqMessages.Should().AllSatisfy(m =>
            {
                m.Source.Should().Be(Iris.Brokers.Models.ReadSource.DeadLetter);
                m.Provider.Should().Be("AmazonSQS");
            });
        }

        [Fact(DisplayName = "Send maps headers to message attributes and drops what the compatibility check dropped", Timeout = 120000)]
        public async Task Send_maps_headers_to_attributes()
        {
            using var client = CreateSqsClient();
            var connection = CreateConnection(client);
            var queueName = "iris-sqs-send-headers-test";
            await client.CreateQueueAsync(queueName, TestContext.Current.CancellationToken);

            var adapter = new BrighterAdapter();
            var request = MessageRequest.Create("OrderPlaced", "{\"i\":1}", generateIrisHeaders: true, "MyApp.OrderPlaced");
            var compatibility = FrameworkCompatibility.Check(adapter, connection, request.Headers.Count);
            compatibility.Supported.Should().BeTrue();
            request.WrapMessage(adapter);
            FrameworkCompatibility.RemoveDropped(request, compatibility);

            await connection.SendAsync(Endpoint(queueName), request);

            var msgs = await Eventually.NonEmptyAsync(
                _ => ((IMessageReceiver)connection).ReceiveAsync(Endpoint(queueName), 10),
                "the sent message becomes visible on the queue",
                TestContext.Current.CancellationToken);

            msgs.Should().ContainSingle();
            msgs[0].Properties.Should().ContainKey("MessageType").WhoseValue.Should().Be("MT_EVENT");
            msgs[0].Properties.Should().ContainKey("iris-key");
            msgs[0].Properties.Should().NotContainKey("cloudEvents_time");
            msgs[0].Properties.Count.Should().Be(10);
        }
    }
}
