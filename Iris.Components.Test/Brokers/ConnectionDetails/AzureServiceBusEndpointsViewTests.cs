using Bunit;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Components.Brokers.ConnectionDetails;
using Iris.Contracts.Brokers.Models;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Iris.Components.Test.Brokers.ConnectionDetails;

public class AzureServiceBusEndpointsViewTests : IrisTestContext
{
    [Fact(DisplayName = "AzureServiceBusEndpointsView shows Queues, Topics and Subscriptions")]
    public void Shows_three_sections()
    {
        var provider = new Provider { Id = Guid.NewGuid(), Name = "AzureServiceBus", Address = "sb://x" };
        var brokerService = Substitute.For<IBrokerService>();
        brokerService.GetEndpointsAsync().Returns(new List<EndpointDetails>
        {
            new() { Name = "orders",         Address = "sb://x", Provider = "AzureServiceBus", Type = "Queue" },
            new() { Name = "events.topic",   Address = "sb://x", Provider = "AzureServiceBus", Type = "Topic" },
            new() { Name = "events.sub-a",   Address = "sb://x", Provider = "AzureServiceBus", Type = "Subscription" },
        });
        Services.AddSingleton(brokerService);

        var cut = Render<AzureServiceBusEndpointsView>(p => p.Add(x => x.Provider, provider));

        cut.Markup.Should().Contain("Queues");
        cut.Markup.Should().Contain("Topics");
        cut.Markup.Should().Contain("Subscriptions");
        cut.Markup.Should().Contain("orders");
        cut.Markup.Should().Contain("events.topic");
        cut.Markup.Should().Contain("events.sub-a");
    }
}
