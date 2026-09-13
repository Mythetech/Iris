using FluentAssertions;
using Iris.Brokers.Frameworks;
using Iris.Brokers.Models;
using Xunit;

namespace Iris.Brokers.Test.Frameworks;

public class FrameworkCompatibilityTests
{
    [Fact(DisplayName = "Rebus on Queue Storage: no headers, fails on the required ones")]
    public void Rebus_QueueStorage_Fails()
    {
        var result = FrameworkCompatibility.Check(new RebusAdapter(), BrokerSenderInterfaceTests.QueueStorage(), 0);

        result.Supported.Should().BeFalse();
        result.Reason.Should().Be("Rebus needs transport headers; dummy cannot carry them.");
    }

    [Fact(DisplayName = "Rebus on SQS: nine string headers fit under ten")]
    public void Rebus_Sqs_Supported()
    {
        var result = FrameworkCompatibility.Check(new RebusAdapter(), BrokerSenderInterfaceTests.Sqs(), 1);

        result.Supported.Should().BeTrue();
        result.DroppedKeys.Should().BeEmpty();
    }

    [Fact(DisplayName = "Brighter on SQS with iris-key: optional headers are dropped to fit, last declared first")]
    public void Brighter_Sqs_DropsOptionalHeaders()
    {
        var adapter = new BrighterAdapter();

        var result = FrameworkCompatibility.Check(adapter, BrokerSenderInterfaceTests.Sqs(), 1);

        result.Supported.Should().BeTrue();
        // Ten Brighter headers plus iris-key is eleven. cloudEvents_time is a Timestamp, which SQS
        // cannot carry, so it goes first; that already brings the count to ten, so nothing else
        // is dropped. The four transport properties are optional and SQS has no carrier, so
        // they are dropped rather than fatal.
        result.DroppedKeys.Where(k => k.Location == KeyLocation.Header).Select(k => k.Name)
            .Should().BeEquivalentTo(new[] { "cloudEvents_time" });
        result.DroppedKeys.Where(k => k.Location == KeyLocation.TransportProperty).Should().HaveCount(4);
        result.DroppedKeys.Should().NotContain(k => k.IsRequired);
    }

    [Fact(DisplayName = "Brighter on SQS with two user headers: one more optional header is dropped, last declared first")]
    public void Brighter_Sqs_TwoUserHeaders_DropsOneMore()
    {
        var result = FrameworkCompatibility.Check(new BrighterAdapter(), BrokerSenderInterfaceTests.Sqs(), 2);

        result.Supported.Should().BeTrue();
        result.DroppedKeys.Where(k => k.Location == KeyLocation.Header).Select(k => k.Name)
            .Should().BeEquivalentTo(new[] { "cloudEvents_time", "cloudEvents_source" });
    }

    [Fact(DisplayName = "Brighter on SQS with ten user headers: the required MessageType cannot fit")]
    public void Brighter_Sqs_TooManyUserHeaders_Fails()
    {
        var result = FrameworkCompatibility.Check(new BrighterAdapter(), BrokerSenderInterfaceTests.Sqs(), 10);

        result.Supported.Should().BeFalse();
        result.Reason.Should().Be("Brighter needs 1 header plus 10 of yours; dummy allows 10.");
    }

    [Fact(DisplayName = "EasyNetQ on Service Bus: Subject is not the AMQP type property, so the required Type fails")]
    public void EasyNetQ_ServiceBus_Fails_OnRequiredType()
    {
        var result = FrameworkCompatibility.Check(new EasyNetQAdapter(), BrokerSenderInterfaceTests.ServiceBus(), 0);

        result.Supported.Should().BeFalse();
        result.Reason.Should().Be("dummy cannot set Type.");
    }

    [Fact(DisplayName = "Brighter on Service Bus: the optional Type is dropped rather than fatal")]
    public void Brighter_ServiceBus_DropsType()
    {
        var result = FrameworkCompatibility.Check(new BrighterAdapter(), BrokerSenderInterfaceTests.ServiceBus(), 0);

        result.Supported.Should().BeTrue();
        result.DroppedKeys.Select(k => k.Property).Should().Contain(TransportProperty.Type);
    }

    [Fact(DisplayName = "EasyNetQ on SQS: no transport properties at all")]
    public void EasyNetQ_Sqs_Fails()
    {
        var result = FrameworkCompatibility.Check(new EasyNetQAdapter(), BrokerSenderInterfaceTests.Sqs(), 0);

        result.Supported.Should().BeFalse();
        result.Reason.Should().Be("EasyNetQ needs native message properties; dummy has none.");
    }

    [Fact(DisplayName = "Wolverine on Queue Storage fails on the required identity")]
    public void Wolverine_QueueStorage_Fails()
    {
        var result = FrameworkCompatibility.Check(new WolverineAdapter(), BrokerSenderInterfaceTests.QueueStorage(), 0);

        result.Supported.Should().BeFalse();
        result.Reason.Should().Be("Wolverine needs native message properties; dummy has none.");
    }

    [Fact(DisplayName = "Body-only frameworks are supported everywhere")]
    public void MassTransit_QueueStorage_Supported()
    {
        var result = FrameworkCompatibility.Check(new MassTransitAdapter(), BrokerSenderInterfaceTests.QueueStorage(), 5);

        result.Supported.Should().BeTrue();
        result.DroppedKeys.Should().BeEmpty();
    }

    [Fact(DisplayName = "RabbitMQ carries everything for every adapter")]
    public void Rabbit_SupportsAll()
    {
        IFramework[] adapters = [new MassTransitAdapter(), new NServiceBusAdapter(), new RebusAdapter(), new BrighterAdapter(), new WolverineAdapter(), new EasyNetQAdapter()];

        foreach (var adapter in adapters)
        {
            var result = FrameworkCompatibility.Check(adapter, BrokerSenderInterfaceTests.Rabbit(), 3);
            result.Supported.Should().BeTrue(adapter.Name);
            result.DroppedKeys.Should().BeEmpty(adapter.Name);
        }
    }

    [Fact(DisplayName = "A user header key SQS cannot spell fails the check before the broker sees it")]
    public void UserHeader_WithInvalidKey_FailsOnSqs()
    {
        var result = FrameworkCompatibility.Check(new RebusAdapter(), BrokerSenderInterfaceTests.Sqs(), new[] { "bad key" });

        result.Supported.Should().BeFalse();
        result.Reason.Should().Be("dummy rejects header name 'bad key'.");
    }

    [Fact(DisplayName = "Valid user header keys count towards the carrier's limit just like a raw count")]
    public void UserHeaders_ValidKeys_CountTowardsTheLimit()
    {
        var result = FrameworkCompatibility.Check(
            new BrighterAdapter(), BrokerSenderInterfaceTests.Sqs(), new[] { "iris-key", "tenant" });

        result.Supported.Should().BeTrue();
        result.DroppedKeys.Where(k => k.Location == KeyLocation.Header).Select(k => k.Name)
            .Should().BeEquivalentTo(new[] { "cloudEvents_time", "cloudEvents_source" });
    }

    [Fact(DisplayName = "RemoveDropped strips dropped headers and clears dropped transport properties")]
    public void RemoveDropped_StripsRequest()
    {
        var adapter = new BrighterAdapter();
        var request = MessageRequest.Create("OrderPlaced", "{}", generateIrisHeaders: true, "MyApp.OrderPlaced");
        request.WrapMessage(adapter);
        var result = FrameworkCompatibility.Check(adapter, BrokerSenderInterfaceTests.Sqs(), 1);

        FrameworkCompatibility.RemoveDropped(request, result);

        request.Headers.Should().NotContainKey("cloudEvents_time");
        request.Headers.Should().ContainKey("MessageType");
        request.Headers.Should().ContainKey("iris-key");
        request.Headers.Count.Should().Be(10);
        request.TransportProperties.SetProperties().Should().BeEmpty();
    }
}
