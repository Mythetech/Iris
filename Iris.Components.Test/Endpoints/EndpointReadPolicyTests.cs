using FluentAssertions;
using Iris.Components.Endpoints;
using Iris.Contracts.Brokers.Models;
using Xunit;

namespace Iris.Components.Test.Endpoints;

public class EndpointReadPolicyTests
{
    private static EndpointDetails Queue(string name = "orders") => new()
    {
        Name = name,
        Address = "amqp://x",
        Provider = "RabbitMq",
        Type = "Queue",
    };

    private static ReaderCapabilitiesDto Capabilities(bool canReceive) =>
        new(CanPeek: true, CanReceive: canReceive, CanPeekDeadLetter: false, CanReceiveDeadLetter: false,
            MaxPeekBatchSize: 10, MaxReceiveBatchSize: 10);

    [Fact(DisplayName = "CanRead is false when the endpoint is null")]
    public void CanRead_false_for_null_endpoint()
    {
        EndpointReadPolicy.CanRead(null, Capabilities(canReceive: true)).Should().BeFalse();
    }

    [Fact(DisplayName = "CanRead is false for non-queue endpoints even when the broker can receive")]
    public void CanRead_false_for_non_queue_endpoint()
    {
        var topic = new EndpointDetails { Name = "orders", Address = "amqp://x", Provider = "RabbitMq", Type = "Topic" };

        EndpointReadPolicy.CanRead(topic, Capabilities(canReceive: true)).Should().BeFalse();
    }

    [Fact(DisplayName = "CanRead is false when capabilities are unavailable")]
    public void CanRead_false_when_capabilities_null()
    {
        EndpointReadPolicy.CanRead(Queue(), null).Should().BeFalse();
    }

    [Fact(DisplayName = "CanRead is false when the broker cannot receive")]
    public void CanRead_false_when_broker_cannot_receive()
    {
        EndpointReadPolicy.CanRead(Queue(), Capabilities(canReceive: false)).Should().BeFalse();
    }

    [Fact(DisplayName = "CanRead is true for a queue whose broker can receive")]
    public void CanRead_true_for_readable_queue()
    {
        EndpointReadPolicy.CanRead(Queue(), Capabilities(canReceive: true)).Should().BeTrue();
    }

    [Fact(DisplayName = "DisabledReason reports an unknown endpoint when null")]
    public void DisabledReason_for_null_endpoint()
    {
        EndpointReadPolicy.DisabledReason(null).Should().Be("Unknown endpoint");
    }

    [Fact(DisplayName = "DisabledReason reports queue-only support for non-queue endpoints")]
    public void DisabledReason_for_non_queue_endpoint()
    {
        var topic = new EndpointDetails { Name = "n", Address = "a", Provider = "p", Type = "Topic" };

        EndpointReadPolicy.DisabledReason(topic).Should().Be("Reading is only supported for queue endpoints");
    }

    [Fact(DisplayName = "DisabledReason reports broker support for queue endpoints")]
    public void DisabledReason_for_queue_endpoint()
    {
        EndpointReadPolicy.DisabledReason(Queue()).Should().Be("Broker does not support reading messages");
    }
}
