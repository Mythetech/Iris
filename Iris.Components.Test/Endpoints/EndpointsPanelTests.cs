using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Components.Endpoints;
using Iris.Components.Messaging;
using Microsoft.AspNetCore.Components;
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
        var cut = Render<EndpointsPanel>();

        cut.FindAll("button.endpoints-panel-row").Should().NotBeEmpty();
    }

    [Fact]
    public async Task ClickingARow_OpensTheDetailsDialog()
    {
        var dialogService = Substitute.For<IDialogService>();
        Services.AddSingleton(dialogService);

        var cut = Render<EndpointsPanel>();

        await cut.FindAll("button.endpoints-panel-row")[0].ClickAsync(new());

        await dialogService.Received(1).ShowAsync<EndpointDetailsDialog>(
            Arg.Any<string>(), Arg.Any<DialogParameters>(), Arg.Any<DialogOptions>());
    }

    [Fact]
    public async Task MoreLink_NavigatesToTheEndpointsPage()
    {
        var cut = Render<EndpointsPanel>();

        await cut.Find("button.endpoints-panel-more").ClickAsync(new());

        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.Uri.Should().Be($"{nav.BaseUri}Endpoints");
    }
    [Fact(DisplayName = "The details dialog is given a Read handler that opens the reader")]
    public async Task Read_from_the_details_dialog_opens_the_reader()
    {
        // EndpointDetailsDialog has always rendered a Read button, and the panel built its
        // DialogParameters without OnReadClicked, so the button invoked an empty
        // EventCallback and the panel's only read path did nothing at all.
        var dialogService = Substitute.For<IDialogService>();
        Services.AddSingleton(dialogService);

        _brokerService.GetReaderCapabilitiesAsync(Arg.Any<string>())
            .Returns(new ReaderCapabilitiesDto(true, true, false, false, 10, 10));

        DialogParameters? captured = null;
        await dialogService.ShowAsync<EndpointDetailsDialog>(
            Arg.Any<string>(),
            Arg.Do<DialogParameters>(p => captured = p),
            Arg.Any<DialogOptions>());

        var cut = Render<EndpointsPanel>();
        await cut.FindAll("button.endpoints-panel-row")[0].ClickAsync(new());

        captured.Should().NotBeNull();

        var onRead = captured!.Get<EventCallback<EndpointDetails>>(nameof(EndpointDetailsDialog.OnReadClicked));
        onRead.HasDelegate.Should().BeTrue("an empty EventCallback is an inert button");

        await cut.InvokeAsync(() => onRead.InvokeAsync(BuildEndpoints(1)[0]));

        await dialogService.Received(1).ShowAsync<MessageReaderDialog>(
            Arg.Any<string>(), Arg.Any<DialogParameters>(), Arg.Any<DialogOptions>());
    }
}
