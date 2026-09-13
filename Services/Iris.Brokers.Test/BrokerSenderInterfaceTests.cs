using Amazon.Runtime;
using Amazon.SQS;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Azure.Storage.Queues;
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
/// Encodes the per-broker send-capability contract in the type system, the way
/// <see cref="BrokerReaderInterfaceTests"/> does for reads. No I/O: dummy clients are
/// enough to construct each connection and assert which carrier interfaces it implements
/// and what limits it declares.
/// </summary>
public class BrokerSenderInterfaceTests
{
    private static ConnectionMetadata DummyMetadata()
    {
        var connector = Substitute.For<IConnector>();
        connector.Provider.Returns("dummy");
        return new ConnectionMetadata { Connector = connector, Address = "http://localhost/" };
    }

    public static RabbitMqConnection Rabbit()
    {
        var client = new EasyNetQ.Management.Client.ManagementClient(
            new Uri("http://localhost:15672"), "guest", "guest");
        return new RabbitMqConnection(DummyMetadata(), client);
    }

    public static AzureServiceBusConnection ServiceBus()
    {
        var admin = new ServiceBusAdministrationClient(
            "Endpoint=sb://localhost;SharedAccessKeyName=k;SharedAccessKey=k;UseDevelopmentEmulator=true;");
        var client = new ServiceBusClient(
            "Endpoint=sb://localhost;SharedAccessKeyName=k;SharedAccessKey=k;UseDevelopmentEmulator=true;");
        return new AzureServiceBusConnection(DummyMetadata(), admin, client);
    }

    public static AzureQueueStorageConnection QueueStorage()
        => new(DummyMetadata(), new QueueServiceClient("UseDevelopmentStorage=true"),
            NullLogger<AzureQueueStorageConnection>.Instance);

    public static AmazonSimpleQueueServiceConnection Sqs()
        => new(DummyMetadata(), new AmazonSQSClient(
            new BasicAWSCredentials("test", "test"),
            new AmazonSQSConfig { ServiceURL = "http://localhost:9324" }));

    [Fact]
    public void RabbitMq_carries_headers_of_every_type_and_all_transport_properties()
    {
        var connection = Rabbit();

        connection.Should().BeAssignableTo<IHeaderCarrier>();
        connection.Should().BeAssignableTo<ITransportPropertyCarrier>();

        var headers = (IHeaderCarrier)connection;
        headers.MaxHeaderCount.Should().Be(int.MaxValue);
        headers.IsValidHeaderKey("rbs2-msg-id").Should().BeTrue();
        headers.IsValidHeaderKey("cloudEvents_type").Should().BeTrue();
        headers.SupportedDataTypes.Should().BeEquivalentTo(Enum.GetValues<HeaderDataType>());

        ((ITransportPropertyCarrier)connection).SupportedProperties
            .Should().BeEquivalentTo(Enum.GetValues<TransportProperty>());
    }

    [Fact]
    public void ServiceBus_carries_headers_and_three_transport_properties()
    {
        var connection = ServiceBus();

        connection.Should().BeAssignableTo<IHeaderCarrier>();
        ((IHeaderCarrier)connection).MaxHeaderCount.Should().Be(int.MaxValue);
        ((IHeaderCarrier)connection).SupportedDataTypes.Should().BeEquivalentTo(Enum.GetValues<HeaderDataType>());

        connection.Should().BeAssignableTo<ITransportPropertyCarrier>();
        // Subject is the AMQP subject field, not the type property, so Type is not carried.
        ((ITransportPropertyCarrier)connection).SupportedProperties.Should().BeEquivalentTo(new[]
        {
            TransportProperty.MessageId,
            TransportProperty.CorrelationId,
            TransportProperty.ContentType,
        });
    }

    [Fact]
    public void Sqs_carries_ten_string_or_integer_attributes_and_no_transport_properties()
    {
        var connection = Sqs();

        connection.Should().BeAssignableTo<IHeaderCarrier>();
        connection.Should().NotBeAssignableTo<ITransportPropertyCarrier>();

        var headers = (IHeaderCarrier)connection;
        headers.MaxHeaderCount.Should().Be(10);
        headers.IsValidHeaderKey("rbs2-msg-id").Should().BeTrue();
        headers.IsValidHeaderKey("cloudEvents_type").Should().BeTrue();
        headers.IsValidHeaderKey("bad key").Should().BeFalse();
        headers.IsValidHeaderKey("AWS.reserved").Should().BeFalse();
        headers.SupportedDataTypes.Should().BeEquivalentTo(new[] { HeaderDataType.String, HeaderDataType.Integer });
    }

    [Fact]
    public void QueueStorage_carries_body_only()
    {
        var connection = QueueStorage();

        connection.Should().NotBeAssignableTo<IHeaderCarrier>();
        connection.Should().NotBeAssignableTo<ITransportPropertyCarrier>();
    }

    [Theory]
    [InlineData("42", HeaderDataType.Integer, 42L)]
    [InlineData("true", HeaderDataType.Boolean, true)]
    [InlineData("plain", HeaderDataType.String, "plain")]
    [InlineData("not-a-number", HeaderDataType.Integer, "not-a-number")]
    public void HeaderValueEncoder_encodes_declared_types_and_falls_back_to_the_string(
        string value, HeaderDataType type, object expected)
    {
        HeaderValueEncoder.Encode(value, type).Should().Be(expected);
    }

    [Fact]
    public void HeaderValueEncoder_encodes_timestamps_as_DateTimeOffset()
    {
        var encoded = HeaderValueEncoder.Encode("2026-09-13T10:11:12.0000000+00:00", HeaderDataType.Timestamp);

        encoded.Should().BeOfType<DateTimeOffset>();
        ((DateTimeOffset)encoded).Should().Be(new DateTimeOffset(2026, 9, 13, 10, 11, 12, TimeSpan.Zero));
    }
}
