using Bunit;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Contracts.Brokers.Models;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;

namespace Iris.Components.Test.Endpoints;

public class EndpointsPageTests : IrisTestContext
{
    private readonly IBrokerService _brokerService = Substitute.For<IBrokerService>();

    public EndpointsPageTests()
    {
        Services.AddSingleton(_brokerService);
        Render<MudPopoverProvider>();
    }

    /// <summary>
    /// The refresh button binds Disabled to Loading. Before this fix, a broker failure
    /// during LoadAsync left Loading stuck true because nothing reset it on the failing
    /// path, permanently disabling the only way to try again short of leaving the page.
    /// The initial load is deliberately kept successful here: an exception thrown out of
    /// OnInitializedAsync fails the render itself in bUnit, which would only prove the page
    /// crashes rather than that Loading recovers, so the failure is exercised through the
    /// refresh path instead.
    /// </summary>
    [Fact(DisplayName = "A failed refresh still clears Loading so the refresh button recovers")]
    public async Task Failed_Refresh_Recovers_Loading_State()
    {
        _brokerService.GetEndpointsAsync().Returns(
            Task.FromResult(new List<EndpointDetails>()),
            Task.FromException<List<EndpointDetails>>(new InvalidOperationException("broker unavailable")));

        var cut = Render<Iris.Components.Endpoints.Pages.Endpoints>();

        cut.Find("button.endpoints-refresh-button").HasAttribute("disabled").Should().BeFalse();

        var refreshButton = cut.Find("button.endpoints-refresh-button");
        var act = async () => await refreshButton.ClickAsync(new MouseEventArgs());

        await act.Should().ThrowAsync<InvalidOperationException>();

        cut.Find("button.endpoints-refresh-button").HasAttribute("disabled").Should().BeFalse();
    }
}
