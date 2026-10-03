using Bunit;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Components.Endpoints;
using Iris.Contracts.Brokers.Models;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Iris.Components.Test.Endpoints;

public class EndpointDetailsViewTests : IrisTestContext
{
    private const string Address = "pubsub.googleapis.com/projects/p";

    private readonly IBrokerService _brokerService = Substitute.For<IBrokerService>();

    public EndpointDetailsViewTests()
    {
        Services.AddSingleton(_brokerService);
        AddPopoverProvider();
    }

    private static EndpointDetails Orders(string type) => new()
    {
        Name = "orders",
        Address = Address,
        Provider = "Google",
        Type = type,
    };

    [Fact(DisplayName = "A topic and a subscription with the same name each show their own properties")]
    public void Same_name_of_a_different_type_loads_its_own_properties()
    {
        _brokerService.GetEndpointPropertiesAsync(Address, "orders", "Topic", Arg.Any<CancellationToken>())
            .Returns(new EndpointPropertiesDto([new EndpointPropertyEntry("Subscriptions", "billing")]));
        _brokerService.GetEndpointPropertiesAsync(Address, "orders", "Subscription", Arg.Any<CancellationToken>())
            .Returns(new EndpointPropertiesDto([new EndpointPropertyEntry("Delivery type", "Pull")]));

        var cut = Render<EndpointDetailsView>(p => p.Add(x => x.Endpoint, Orders("Topic")));
        cut.Markup.Should().Contain("Subscriptions");

        cut.Render(p => p.Add(x => x.Endpoint, Orders("Subscription")));

        cut.Markup.Should().Contain("Delivery type");
        cut.Markup.Should().NotContain("Subscriptions");
    }
}
