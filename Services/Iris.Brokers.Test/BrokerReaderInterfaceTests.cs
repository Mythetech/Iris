using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Azure.Storage.Queues;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using FluentAssertions;
using Iris.Brokers.Amazon;
using Iris.Brokers.Azure;
using Iris.Brokers.Models;
using Iris.Brokers.RabbitMQ;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Iris.Brokers.Test;

/// <summary>
/// These tests encode the per-broker read-capability contract in the type
/// system. They deliberately avoid any I/O — instantiating the connection
/// classes with dummy clients is enough to assert which <c>IMessage*</c>
/// interfaces each broker does (and does not) implement. If someone removes
/// a capability by mistake, or sneaks in a God-interface implementation on
/// a broker that shouldn't have it, these tests fail immediately.
/// </summary>
public class BrokerReaderInterfaceTests
{
    private static ConnectionMetadata DummyMetadata()
    {
        var connector = Substitute.For<IConnector>();
        connector.Provider.Returns("dummy");
        return new ConnectionMetadata { Connector = connector, Address = "http://localhost/" };
    }

    [Fact]
    public void RabbitMqConnection_implements_all_four_read_interfaces()
    {
        var client = new EasyNetQ.Management.Client.ManagementClient(
            new Uri("http://localhost:15672"), "guest", "guest");
        var metadata = DummyMetadata();
        metadata.Connector = new RabbitMqConnector();

        var connection = new RabbitMqConnection(metadata, client);

        // RabbitMQ supports dead-lettering natively via DLX (dead-letter exchanges);
        // the DLQ is just another named queue discovered through the source queue's
        // x-dead-letter-exchange argument and the bindings on that exchange. Unlike
        // ASB's first-class $DeadLetterQueue sub-entity, it's a per-queue runtime
        // configuration — but from the caller's perspective the capability is the
        // same, so the interfaces are implemented.
        connection.Should().BeAssignableTo<IMessagePeeker>();
        connection.Should().BeAssignableTo<IMessageReceiver>();
        connection.Should().BeAssignableTo<IDeadLetterPeeker>();
        connection.Should().BeAssignableTo<IDeadLetterReceiver>();
    }

    [Fact]
    public void AzureServiceBusConnection_implements_all_four_read_interfaces()
    {
        var metadata = DummyMetadata();
        var admin = new ServiceBusAdministrationClient(
            "Endpoint=sb://localhost;SharedAccessKeyName=k;SharedAccessKey=k;UseDevelopmentEmulator=true;");
        var client = new ServiceBusClient(
            "Endpoint=sb://localhost;SharedAccessKeyName=k;SharedAccessKey=k;UseDevelopmentEmulator=true;");

        var connection = new AzureServiceBusConnection(metadata, admin, client);

        connection.Should().BeAssignableTo<IMessagePeeker>();
        connection.Should().BeAssignableTo<IMessageReceiver>();
        connection.Should().BeAssignableTo<IDeadLetterPeeker>();
        connection.Should().BeAssignableTo<IDeadLetterReceiver>();
    }

    [Fact]
    public void AzureQueueStorageConnection_implements_peek_and_receive_only()
    {
        var metadata = DummyMetadata();
        var client = new QueueServiceClient("UseDevelopmentStorage=true");

        var connection = new AzureQueueStorageConnection(
            metadata, client, NullLogger<AzureQueueStorageConnection>.Instance);

        connection.Should().BeAssignableTo<IMessagePeeker>();
        connection.Should().BeAssignableTo<IMessageReceiver>();
        connection.Should().NotBeAssignableTo<IDeadLetterPeeker>();
        connection.Should().NotBeAssignableTo<IDeadLetterReceiver>();
    }

    [Fact]
    public void AmazonSqsConnection_implements_receive_and_dlq_receive_only_no_peek()
    {
        var metadata = DummyMetadata();
        var client = new AmazonSQSClient(
            new BasicAWSCredentials("test", "test"),
            new AmazonSQSConfig { ServiceURL = "http://localhost:9324" });

        var connection = new AmazonSimpleQueueServiceConnection(metadata, client);

        // SQS has no true peek — ReceiveMessage always starts a visibility timer.
        // Encoding that in the type system: no IMessagePeeker / no IDeadLetterPeeker.
        connection.Should().NotBeAssignableTo<IMessagePeeker>();
        connection.Should().NotBeAssignableTo<IDeadLetterPeeker>();
        connection.Should().BeAssignableTo<IMessageReceiver>();
        connection.Should().BeAssignableTo<IDeadLetterReceiver>();
    }
}
