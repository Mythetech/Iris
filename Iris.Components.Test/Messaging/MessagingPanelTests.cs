using Bunit;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Components.Messaging;
using Iris.Contracts.Brokers.Models;
using Iris.Contracts.Results;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using Xunit;

namespace Iris.Components.Test.Messaging;

public class MessagingPanelTests : IrisTestContext
{
    private readonly IBrokerService _brokerService = Substitute.For<IBrokerService>();
    private readonly IMessageSendOrchestrator _orchestrator = Substitute.For<IMessageSendOrchestrator>();

    public MessagingPanelTests()
    {
        _brokerService.GetProvidersAsync().Returns(new List<Provider>
        {
            new() { Name = "RabbitMq", Address = "http://127.0.0.1:15672" },
        });

        _brokerService.GetEndpointsAsync().Returns(new List<EndpointDetails>
        {
            new() { Name = "orders", Address = "http://127.0.0.1:15672", Provider = "RabbitMq", Type = "Queue" },
        });

        _orchestrator
            .SendAsync(Arg.Any<SendContext>(), Arg.Any<IProgress<Result<bool>>?>(), Arg.Any<CancellationToken>())
            .Returns(new Success<bool>(true));

        Services.AddSingleton(_brokerService);
        Services.AddSingleton(_orchestrator);

        // The panel's ProviderSelector, EndpointSelector, and Framework fields are
        // MudSelect, which registers a popover on init and throws if none is mounted.
        JSInterop.Setup<int>("mudpopoverHelper.countProviders", _ => true);
        RenderComponent<MudPopoverProvider>();
    }

    [Fact]
    public void Renders_ComposeFields_NotAnOpenPageButton()
    {
        var cut = RenderComponent<MessagingPanel>();

        cut.Markup.Should().NotContain("New Message");
        cut.Markup.Should().Contain("Select Connection");
        cut.Markup.Should().Contain("Select Endpoint");
    }

    [Fact]
    public void Keeps_TheFooterPageLink()
    {
        var cut = RenderComponent<MessagingPanel>();

        cut.Markup.Should().Contain("Open Messaging Page");
    }

    [Fact]
    public async Task InvalidJson_BlocksSend_AndShowsAnError()
    {
        var cut = RenderComponent<MessagingPanel>();

        await cut.Find("textarea").InputAsync(new() { Value = "{ not json" });
        await cut.Find("button.messaging-panel-send").ClickAsync(new());

        await _orchestrator.DidNotReceive().SendAsync(
            Arg.Any<SendContext>(), Arg.Any<IProgress<Result<bool>>?>(), Arg.Any<CancellationToken>());

        cut.Markup.Should().Contain("not valid JSON");
    }

    [Fact]
    public async Task Send_PassesTheComposedJsonToTheOrchestrator()
    {
        var cut = RenderComponent<MessagingPanel>();

        await cut.Find("textarea").InputAsync(new() { Value = "{\"orderId\":\"1\"}" });
        await cut.Find("button.messaging-panel-send").ClickAsync(new());

        await _orchestrator.Received(1).SendAsync(
            Arg.Is<SendContext>(c => c.Json == "{\"orderId\":\"1\"}"),
            Arg.Any<IProgress<Result<bool>>?>(),
            Arg.Any<CancellationToken>());
    }
}
