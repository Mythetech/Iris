using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Azure;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Iris.Integration.Tests.Fixtures;

namespace Iris.Integration.Tests.Brokers
{
    /// <summary>
    /// Emulator-backed tests for <see cref="AzureServiceBusConnection"/>, run against the
    /// OpenServiceBus emulator managed by <see cref="AzureServiceBusContainerFixture"/>.
    ///
    /// Queues the fixture seeds:
    /// - <c>iris-main-test</c>: MaxDeliveryCount=10. Used for peek/receive tests.
    /// - <c>iris-dlq-test</c>:  MaxDeliveryCount=1. Used for DLQ tests.
    /// </summary>
    [Trait("Category", "Container")]
    [Collection("AzureServiceBus")]
    public class AzureServiceBusContainerTests
    {
        private const string MainQueue = AzureServiceBusContainerFixture.MainQueue;
        private const string DlqQueue = AzureServiceBusContainerFixture.DlqQueue;

        private readonly AzureServiceBusContainerFixture _emulator;

        public AzureServiceBusContainerTests(AzureServiceBusContainerFixture emulator)
        {
            _emulator = emulator;
        }

        // The emulator's port is assigned by Docker, so this is only known once it is running.
        private string ConnectionString => _emulator.ConnectionString;

        private AzureServiceBusConnection CreateConnection()
        {
            var metadata = new ConnectionMetadata
            {
                Connector = new AzureConnector(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance),
                Address = ConnectionString,
            };
            var admin = new ServiceBusAdministrationClient(ConnectionString);
            var client = new ServiceBusClient(ConnectionString);
            return new AzureServiceBusConnection(metadata, admin, client);
        }

        private static EndpointDetails Endpoint(string queueName, string address) => new()
        {
            Provider = "AzureServiceBus",
            Address = address,
            Type = "Queue",
            Name = queueName,
        };

        // No Timeout: this test awaits nothing, so there is nothing for one to interrupt.
        [Fact(DisplayName = "Can connect to Azure Service Bus emulator")]
        public async Task Can_Construct_Connection_From_Emulator()
        {
            var connection = CreateConnection();

            connection.Should().NotBeNull();
            connection.Address.Should().Be(ConnectionString);
            await Task.CompletedTask;
        }

        [Fact(DisplayName = "Peek returns sent messages without removing them", Timeout = 300000)]
        public async Task Peek_is_non_destructive()
        {
            var connection = CreateConnection();
            await using var seeder = new ServiceBusClient(ConnectionString);
            await using var sender = seeder.CreateSender(MainQueue);

            // Drain in case previous tests left residue.
            var drainer = (IMessageReceiver)connection;
            await drainer.ReceiveAsync(Endpoint(MainQueue, ConnectionString), 50, TestContext.Current.CancellationToken);

            await sender.SendMessageAsync(new ServiceBusMessage("{\"i\":1}"), TestContext.Current.CancellationToken);
            await sender.SendMessageAsync(new ServiceBusMessage("{\"i\":2}"), TestContext.Current.CancellationToken);
            await sender.SendMessageAsync(new ServiceBusMessage("{\"i\":3}"), TestContext.Current.CancellationToken);

            var peeker = (IMessagePeeker)connection;
            var first = await peeker.PeekAsync(Endpoint(MainQueue, ConnectionString), 10, TestContext.Current.CancellationToken);
            var second = await peeker.PeekAsync(Endpoint(MainQueue, ConnectionString), 10, TestContext.Current.CancellationToken);

            first.Should().HaveCountGreaterThanOrEqualTo(3);
            second.Select(m => m.Body).Should().Contain(first.Select(m => m.Body));
        }

        [Fact(DisplayName = "Receive removes messages from the main queue", Timeout = 300000)]
        public async Task Receive_is_destructive()
        {
            var connection = CreateConnection();
            await using var seeder = new ServiceBusClient(ConnectionString);
            await using var sender = seeder.CreateSender(MainQueue);

            // Drain first.
            var receiver = (IMessageReceiver)connection;
            await receiver.ReceiveAsync(Endpoint(MainQueue, ConnectionString), 50, TestContext.Current.CancellationToken);

            await sender.SendMessageAsync(new ServiceBusMessage("{\"i\":1}"), TestContext.Current.CancellationToken);
            await sender.SendMessageAsync(new ServiceBusMessage("{\"i\":2}"), TestContext.Current.CancellationToken);
            await sender.SendMessageAsync(new ServiceBusMessage("{\"i\":3}"), TestContext.Current.CancellationToken);

            // Accumulated across attempts rather than slept on, because receive is
            // destructive: a partial batch is gone whether or not the test counted it, so a
            // single read after a fixed pause is the one shape that can lose messages.
            var received = new List<ReceivedMessage>();
            await Eventually.Async(
                async _ =>
                {
                    received.AddRange(await receiver.ReceiveAsync(Endpoint(MainQueue, ConnectionString), 10));
                    return received;
                },
                batch => batch.Count >= 3,
                "all three sent messages have been received",
                TestContext.Current.CancellationToken);

            received.Count.Should().BeGreaterThanOrEqualTo(3);

            var peeker = (IMessagePeeker)connection;
            var afterPeek = await peeker.PeekAsync(Endpoint(MainQueue, ConnectionString), 10, TestContext.Current.CancellationToken);
            afterPeek.Should().BeEmpty();
        }

        [Fact(DisplayName = "Dead-lettered messages are readable via ReceiveDeadLetterAsync", Timeout = 300000)]
        public async Task DeadLetter_receive_reads_from_dlq_subqueue()
        {
            var connection = CreateConnection();
            var dlqReceiver = (IDeadLetterReceiver)connection;

            // Drain the DLQ first in case prior tests left residue.
            await dlqReceiver.ReceiveDeadLetterAsync(Endpoint(DlqQueue, ConnectionString), 50, TestContext.Current.CancellationToken);

            await using var seeder = new ServiceBusClient(ConnectionString);
            await using var sender = seeder.CreateSender(DlqQueue);
            await sender.SendMessageAsync(new ServiceBusMessage("{\"will\":\"dead-letter\"}"), TestContext.Current.CancellationToken);

            await using var peekLock = seeder.CreateReceiver(DlqQueue, new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
            });

            var attempt = await peekLock.ReceiveMessageAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            attempt.Should().NotBeNull();
            await peekLock.AbandonMessageAsync(attempt!, cancellationToken: TestContext.Current.CancellationToken);

            // Abandoning is what increments the delivery count, so each attempt that still
            // finds the message has to abandon it again to make progress. The poll ends when
            // the main queue stops offering it, which is the observable fact the assertion
            // below depends on. The old fixed two second wait was sized to the emulator's
            // lock duration and to nothing else.
            var secondAttempt = await Eventually.Async(
                async _ =>
                {
                    var message = await peekLock.ReceiveMessageAsync(TimeSpan.FromSeconds(3));

                    if (message is not null)
                        await peekLock.AbandonMessageAsync(message);

                    return message;
                },
                message => message is null,
                "the abandoned message stops being delivered from the main queue",
                TestContext.Current.CancellationToken);

            secondAttempt.Should().BeNull("the message should have moved to the DLQ sub-queue");

            var dlqMessages = await dlqReceiver.ReceiveDeadLetterAsync(Endpoint(DlqQueue, ConnectionString), 10, TestContext.Current.CancellationToken);

            dlqMessages.Should().NotBeEmpty("the abandoned message should have been dead-lettered");
            dlqMessages.Should().AllSatisfy(m =>
            {
                m.Source.Should().Be(ReadSource.DeadLetter);
                m.Provider.Should().Be("AzureServiceBus");
            });
        }

        [Fact(DisplayName = "Send maps headers to ApplicationProperties and sets only the three carried properties", Timeout = 300000)]
        public async Task Send_maps_headers_and_transport_properties()
        {
            var connection = CreateConnection();

            // Drain first so the SDK peek below is guaranteed to see only this test's message.
            var drainer = (IMessageReceiver)connection;
            await drainer.ReceiveAsync(Endpoint(MainQueue, ConnectionString), 50, TestContext.Current.CancellationToken);

            // Brighter rather than EasyNetQ: Service Bus has no AMQP type property (Subject is
            // the subject field, which no consumer reading type ever sees), so EasyNetQ's
            // required Type fails the compatibility check here. Brighter's Type is optional
            // and simply dropped, leaving the headers and the three carried properties.
            var adapter = new Iris.Brokers.Frameworks.BrighterAdapter();
            var request = MessageRequest.Create("OrderPlaced", "{\"i\":1}", generateIrisHeaders: false, "MyApp.OrderPlaced",
                properties: new Dictionary<string, string> { [Iris.Brokers.Frameworks.BrighterAdapter.TopicKey] = MainQueue },
                messageAssemblyName: "MyApp");

            var compatibility = FrameworkCompatibility.Check(adapter, connection, request.Headers.Count);
            compatibility.Supported.Should().BeTrue();
            request.WrapMessage(adapter);
            FrameworkCompatibility.RemoveDropped(request, compatibility);

            await connection.SendAsync(Endpoint(MainQueue, ConnectionString), request);

            // AzureServiceBusConnection.Map does not surface Subject or ApplicationProperties
            // absence on the ReceivedMessage shape, so this inspects the emulator directly with
            // the SDK to verify what actually landed on the wire.
            await using var client = new ServiceBusClient(ConnectionString);
            await using var receiver = client.CreateReceiver(MainQueue);
            var peeked = await receiver.PeekMessageAsync(cancellationToken: TestContext.Current.CancellationToken);

            peeked.Should().NotBeNull();
            peeked!.ContentType.Should().Be("application/json");
            peeked.Subject.Should().BeNull();
            peeked.MessageId.Should().NotBeNullOrWhiteSpace();
            peeked.ApplicationProperties.Should().ContainKey("MessageType").WhoseValue.Should().Be("MT_EVENT");
            peeked.ApplicationProperties.Should().ContainKey("cloudEvents_type");
        }
    }
}
