using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Amazon;
using Iris.Brokers.Models;

namespace Iris.Integration.Tests.Brokers
{
    /// <summary>
    /// Emulator-backed integration tests for <see cref="AmazonSimpleQueueServiceConnection"/>.
    /// Uses ElasticMQ (Apache 2.0, single-container, SQS-compatible) so the
    /// suite can run committable tests without real AWS credentials.
    /// </summary>
    [Trait("Category", "Container")]
    public class AmazonSqsContainerTests : IAsyncLifetime
    {
        private const ushort SqsPort = 9324;
        private readonly IContainer _container;

        public AmazonSqsContainerTests()
        {
            _container = new ContainerBuilder()
                .WithImage("softwaremill/elasticmq-native:latest")
                .WithPortBinding(SqsPort, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(SqsPort))
                .Build();
        }

        public Task InitializeAsync() => _container.StartAsync();
        public Task DisposeAsync() => _container.DisposeAsync().AsTask();

        private AmazonSQSClient CreateSqsClient()
        {
            return new AmazonSQSClient(
                new BasicAWSCredentials("test", "test"),
                new AmazonSQSConfig
                {
                    ServiceURL = $"http://localhost:{_container.GetMappedPublicPort(SqsPort)}",
                    AuthenticationRegion = "elasticmq",
                });
        }

        private AmazonSimpleQueueServiceConnection CreateConnection(AmazonSQSClient client)
        {
            var connector = new AmazonWebServicesConnector();
            var metadata = new ConnectionMetadata
            {
                Connector = connector,
                Address = $"http://localhost:{_container.GetMappedPublicPort(SqsPort)}",
            };
            return new AmazonSimpleQueueServiceConnection(metadata, client);
        }

        private static EndpointDetails Endpoint(string queueName) => new()
        {
            Provider = "Amazon",
            Address = "elasticmq",
            Type = "Queue",
            Name = queueName,
        };

        [Fact(DisplayName = "AmazonSqsConnection implements receive + dlq receive only", Timeout = 120000)]
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
            await client.CreateQueueAsync(queueName);

            await client.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = (await client.GetQueueUrlAsync(queueName)).QueueUrl,
                MessageBody = "{\"i\":1}",
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["customAttr"] = new() { DataType = "String", StringValue = "hello" },
                },
            });

            // Give the queue a beat to make the message visible.
            await Task.Delay(500);

            var msgs = await receiver.ReceiveAsync(Endpoint(queueName), 10);

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
            await client.CreateQueueAsync(queueName);

            var dlqMsgs = await dlqReceiver.ReceiveDeadLetterAsync(Endpoint(queueName), 10);

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

            var dlqUrlResp = await client.CreateQueueAsync(dlqName);
            var dlqAttrs = await client.GetQueueAttributesAsync(new GetQueueAttributesRequest
            {
                QueueUrl = dlqUrlResp.QueueUrl,
                AttributeNames = new List<string> { "QueueArn" },
            });
            var dlqArn = dlqAttrs.Attributes["QueueArn"];

            var mainUrlResp = await client.CreateQueueAsync(new CreateQueueRequest
            {
                QueueName = mainName,
                Attributes = new Dictionary<string, string>
                {
                    ["RedrivePolicy"] =
                        $"{{\"deadLetterTargetArn\":\"{dlqArn}\",\"maxReceiveCount\":1}}",
                },
            });
            var mainUrl = mainUrlResp.QueueUrl;

            // Send a message to the main queue, then drive its delivery
            // count past maxReceiveCount=1 so ElasticMQ moves it to the DLQ.
            // Step 1: send.
            await client.SendMessageAsync(mainUrl, "{\"will\":\"dead-letter\"}");
            await Task.Delay(300);

            // Step 2: receive once with a very short visibility timeout so
            // we can re-receive without waiting. Don't delete — let the
            // visibility timer lapse so the broker re-attempts delivery.
            var firstAttempt = await client.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = mainUrl,
                MaxNumberOfMessages = 1,
                VisibilityTimeout = 1,
                WaitTimeSeconds = 1,
            });
            firstAttempt.Messages.Should().NotBeNull().And.NotBeEmpty();

            // Wait for visibility to expire so the message becomes deliverable
            // again — this is the trigger for the redrive policy.
            await Task.Delay(2000);

            // Step 3: receive again — ElasticMQ counts this as the 2nd
            // delivery attempt and moves the message to the DLQ instead of
            // returning it. The receive call returns empty.
            var secondAttempt = await client.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = mainUrl,
                MaxNumberOfMessages = 1,
                VisibilityTimeout = 1,
                WaitTimeSeconds = 1,
            });
            // The message should now be in the DLQ; the second attempt
            // returns nothing or returns and we'd receive it again, but
            // either way the next call to ReceiveDeadLetterAsync should
            // find it.
            await Task.Delay(500);

            var dlqMessages = await dlqReceiver.ReceiveDeadLetterAsync(Endpoint(mainName), 10);

            dlqMessages.Should().NotBeEmpty(
                "the message should have been redriven to the DLQ after exceeding maxReceiveCount=1");
            dlqMessages.Should().AllSatisfy(m =>
            {
                m.Source.Should().Be(Iris.Brokers.Models.ReadSource.DeadLetter);
                m.Provider.Should().Be("AmazonSQS");
            });
        }
    }
}
