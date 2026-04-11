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

public class DefaultEndpointsViewTests : TestContext
{
    public DefaultEndpointsViewTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact(DisplayName = "DefaultEndpointsView lists endpoints for the supplied provider")]
    public void DefaultEndpointsView_renders_provider_endpoints()
    {
        var provider = new Provider { Id = Guid.NewGuid(), Name = "RabbitMq", Address = "amqp://x" };
        var brokerService = Substitute.For<IBrokerService>();
        brokerService.GetEndpointsAsync().Returns(new List<EndpointDetails>
        {
            new() { Name = "orders", Address = "amqp://x", Provider = "RabbitMq", Type = "Queue" },
            new() { Name = "events", Address = "amqp://x", Provider = "RabbitMq", Type = "Exchange" },
            new() { Name = "other",  Address = "amqp://y", Provider = "RabbitMq", Type = "Queue" },
        });
        Services.AddSingleton(brokerService);

        var cut = RenderComponent<DefaultEndpointsView>(p => p.Add(x => x.Provider, provider));

        cut.Markup.Should().Contain("orders");
        cut.Markup.Should().Contain("events");
        cut.Markup.Should().NotContain("other");
    }

    [Fact(DisplayName = "DefaultEndpointsView shows an empty state when no endpoints exist")]
    public void DefaultEndpointsView_shows_empty_state()
    {
        var provider = new Provider { Id = Guid.NewGuid(), Name = "RabbitMq", Address = "amqp://x" };
        var brokerService = Substitute.For<IBrokerService>();
        brokerService.GetEndpointsAsync().Returns(new List<EndpointDetails>());
        Services.AddSingleton(brokerService);

        var cut = RenderComponent<DefaultEndpointsView>(p => p.Add(x => x.Provider, provider));

        cut.Markup.Should().Contain("No endpoints");
    }
}
