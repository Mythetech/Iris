using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Components.Endpoints;
using Iris.Contracts.Brokers.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using Xunit;

namespace Iris.Components.Test.Endpoints;

public class EndpointsPanelTests : IrisTestContext
{
    private readonly IBrokerService _brokerService = Substitute.For<IBrokerService>();

    public EndpointsPanelTests()
    {
        _brokerService.GetEndpointsAsync().Returns(BuildEndpoints(12));
        Services.AddSingleton(_brokerService);
    }

    private static List<EndpointDetails> BuildEndpoints(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new EndpointDetails
            {
                Name = $"queue-{i}",
                Address = "http://127.0.0.1:15672",
                Provider = "RabbitMq",
                Type = "Queue",
            })
            .ToList();

    [Fact]
    public void Renders_EndpointRows_AsClickableItems()
    {
        var cut = RenderComponent<EndpointsPanel>();

        cut.FindAll("button.endpoints-panel-row").Should().NotBeEmpty();
    }

    [Fact]
    public async Task ClickingARow_OpensTheDetailsDialog()
    {
        var dialogService = Substitute.For<IDialogService>();
        Services.AddSingleton(dialogService);

        var cut = RenderComponent<EndpointsPanel>();

        await cut.FindAll("button.endpoints-panel-row")[0].ClickAsync(new());

        await dialogService.Received(1).ShowAsync<EndpointDetailsDialog>(
            Arg.Any<string>(), Arg.Any<DialogParameters>(), Arg.Any<DialogOptions>());
    }

    [Fact]
    public async Task MoreLink_NavigatesToTheEndpointsPage()
    {
        var cut = RenderComponent<EndpointsPanel>();

        await cut.Find("button.endpoints-panel-more").ClickAsync(new());

        var nav = Services.GetRequiredService<FakeNavigationManager>();
        nav.Uri.Should().Be($"{nav.BaseUri}Endpoints");
    }
}
