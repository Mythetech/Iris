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
/// Every connection must expose a stable, unique <see cref="IConnection.Id"/>
/// generated at construction time. The Id is the URL-safe handle used by the
/// new /connections/{id} page; it must be set without any I/O.
/// </summary>
public class ConnectionIdentityTests
{
    private static ConnectionMetadata DummyMetadata()
    {
        var connector = Substitute.For<IConnector>();
        connector.Provider.Returns("dummy");
        return new ConnectionMetadata { Connector = connector, Address = "http://localhost/" };
    }

    [Fact]
    public void RabbitMqConnection_assigns_a_non_empty_Id()
    {
        var client = new EasyNetQ.Management.Client.ManagementClient(
            new Uri("http://localhost:15672"), "guest", "guest");
        var metadata = DummyMetadata();
        metadata.Connector = new RabbitMqConnector();

        var connection = new RabbitMqConnection(metadata, client);

        connection.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void AzureServiceBusConnection_assigns_a_non_empty_Id()
    {
        var metadata = DummyMetadata();
        var admin = new ServiceBusAdministrationClient(
            "Endpoint=sb://localhost;SharedAccessKeyName=k;SharedAccessKey=k;UseDevelopmentEmulator=true;");
        var client = new ServiceBusClient(
            "Endpoint=sb://localhost;SharedAccessKeyName=k;SharedAccessKey=k;UseDevelopmentEmulator=true;");

        var connection = new AzureServiceBusConnection(metadata, admin, client);

        connection.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void AzureQueueStorageConnection_assigns_a_non_empty_Id()
    {
        var metadata = DummyMetadata();
        var client = new QueueServiceClient("UseDevelopmentStorage=true");

        var connection = new AzureQueueStorageConnection(
            metadata, client, NullLogger<AzureQueueStorageConnection>.Instance);

        connection.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void AmazonSqsConnection_assigns_a_non_empty_Id()
    {
        var metadata = DummyMetadata();
        var client = new AmazonSQSClient(
            new BasicAWSCredentials("test", "test"),
            new AmazonSQSConfig { ServiceURL = "http://localhost:9324" });

        var connection = new AmazonSimpleQueueServiceConnection(metadata, client);

        connection.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Two_RabbitMqConnections_get_different_Ids()
    {
        var client = new EasyNetQ.Management.Client.ManagementClient(
            new Uri("http://localhost:15672"), "guest", "guest");

        var a = new RabbitMqConnection(DummyMetadata(), client);
        var b = new RabbitMqConnection(DummyMetadata(), client);

        a.Id.Should().NotBe(b.Id);
    }
}
