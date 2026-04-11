using Bunit;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Components.Brokers.ConnectionDetails;
using Iris.Contracts.Brokers.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;
using Xunit;

namespace Iris.Components.Test.Brokers.ConnectionDetails;

public class RabbitMqEndpointsViewTests : TestContext
{
    public RabbitMqEndpointsViewTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact(DisplayName = "RabbitMqEndpointsView shows Queues and Exchanges sections with the right items")]
    public void Shows_queues_and_exchanges()
    {
        var provider = new Provider { Id = Guid.NewGuid(), Name = "RabbitMq", Address = "amqp://x" };
        var brokerService = Substitute.For<IBrokerService>();
        brokerService.GetEndpointsAsync().Returns(new List<EndpointDetails>
        {
            new() { Name = "orders",      Address = "amqp://x", Provider = "RabbitMq", Type = "Queue" },
            new() { Name = "audit",       Address = "amqp://x", Provider = "RabbitMq", Type = "Queue" },
            new() { Name = "events.fan",  Address = "amqp://x", Provider = "RabbitMq", Type = "Exchange" },
        });
        Services.AddSingleton(brokerService);

        var cut = RenderComponent<RabbitMqEndpointsView>(p => p.Add(x => x.Provider, provider));

        cut.Markup.Should().Contain("Queues");
        cut.Markup.Should().Contain("Exchanges");
        cut.Markup.Should().Contain("orders");
        cut.Markup.Should().Contain("audit");
        cut.Markup.Should().Contain("events.fan");
    }
}
