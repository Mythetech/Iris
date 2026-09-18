using Bunit;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Components.Messaging;
using Iris.Contracts.Brokers.Models;
using Iris.Contracts.Messaging.Frameworks;
using Iris.Contracts.Results;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Xunit;

namespace Iris.Components.Test.Messaging;

public class MessagingPanelTests : IrisTestContext
{
    private readonly IBrokerService _brokerService = Substitute.For<IBrokerService>();
    private readonly IMessageSendOrchestrator _orchestrator = Substitute.For<IMessageSendOrchestrator>();
    private readonly IFrameworkCatalog _catalog = Substitute.For<IFrameworkCatalog>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();

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

        _catalog.GetFrameworksAsync(Arg.Any<string?>(), Arg.Any<int>()).Returns(new List<FrameworkDescriptor>
        {
            new("MassTransit", []),
            new("Rebus", [], Supported: false, UnsupportedReason: "Rebus needs transport headers; RabbitMq cannot carry them."),
        });
        Services.AddSingleton(_catalog);
        Services.AddSingleton(Substitute.For<IMessageBus>());
        Services.AddSingleton<MessagingSettings>();
        Services.AddScoped<MessageState>();

        Services.AddSingleton(_brokerService);
        Services.AddSingleton(_orchestrator);
        // Registered after AddMudServices so the panel resolves this substitute rather
        // than MudBlazor's own SnackbarService.
        Services.AddSingleton(_snackbar);

        // The panel's ProviderSelector, EndpointSelector, and Framework fields are
        // MudSelect, which registers a popover on init and throws if none is mounted.
        JSInterop.Setup<int>("mudpopoverHelper.countProviders", _ => true).SetResult(1);
        Render<MudPopoverProvider>();
    }

    [Fact]
    public void Renders_ComposeFields_NotAnOpenPageButton()
    {
        var cut = Render<MessagingPanel>();

        cut.Markup.Should().NotContain("New Message");
        cut.Markup.Should().Contain("Select Connection");
        cut.Markup.Should().Contain("Select Endpoint");
    }

    [Fact]
    public void Keeps_TheFooterPageLink()
    {
        var cut = Render<MessagingPanel>();

        cut.Markup.Should().Contain("Open Messaging Page");
    }

    [Fact]
    public async Task InvalidJson_BlocksSend_AndShowsAnError()
    {
        var cut = Render<MessagingPanel>();

        await SelectProviderAndEndpointAsync(cut);

        await cut.Find("textarea").InputAsync(new() { Value = "{ not json" });
        await cut.Find("button.messaging-panel-send").ClickAsync(new());

        await _orchestrator.DidNotReceive().SendAsync(
            Arg.Any<SendContext>(), Arg.Any<IProgress<Result<bool>>?>(), Arg.Any<CancellationToken>());

        cut.Markup.Should().Contain("not valid JSON");
    }

    [Fact]
    public async Task Send_PassesTheComposedJsonToTheOrchestrator()
    {
        var cut = Render<MessagingPanel>();

        await SelectProviderAndEndpointAsync(cut);

        await cut.Find("textarea").InputAsync(new() { Value = "{\"orderId\":\"1\"}" });
        await cut.Find("button.messaging-panel-send").ClickAsync(new());

        await _orchestrator.Received(1).SendAsync(
            Arg.Is<SendContext>(c => c.Json == "{\"orderId\":\"1\"}"),
            Arg.Any<IProgress<Result<bool>>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_WithNothingSelected_BlocksSend_AndShowsAnError()
    {
        var cut = Render<MessagingPanel>();

        await cut.Find("textarea").InputAsync(new() { Value = "{\"orderId\":\"1\"}" });
        await cut.Find("button.messaging-panel-send").ClickAsync(new());

        await _orchestrator.DidNotReceive().SendAsync(
            Arg.Any<SendContext>(), Arg.Any<IProgress<Result<bool>>?>(), Arg.Any<CancellationToken>());

        cut.Markup.Should().Contain("Select a connection before sending");
    }

    [Fact]
    public async Task Send_WithProviderButNoEndpoint_BlocksSend_AndShowsAnError()
    {
        var cut = Render<MessagingPanel>();

        var provider = new Provider { Name = "RabbitMq", Address = "http://127.0.0.1:15672" };
        await cut.InvokeAsync(() => cut.FindComponent<ProviderSelector>().Instance.ValueChanged.InvokeAsync(provider));

        await cut.Find("textarea").InputAsync(new() { Value = "{\"orderId\":\"1\"}" });
        await cut.Find("button.messaging-panel-send").ClickAsync(new());

        await _orchestrator.DidNotReceive().SendAsync(
            Arg.Any<SendContext>(), Arg.Any<IProgress<Result<bool>>?>(), Arg.Any<CancellationToken>());

        cut.Markup.Should().Contain("Select an endpoint before sending");
    }

    [Fact]
    public async Task Send_PassesIsolatedRepeatAndDelay_SoADrawerSendNeverInheritsPageState()
    {
        var cut = Render<MessagingPanel>();

        await SelectProviderAndEndpointAsync(cut);

        await cut.Find("textarea").InputAsync(new() { Value = "{\"orderId\":\"1\"}" });
        await cut.Find("button.messaging-panel-send").ClickAsync(new());

        await _orchestrator.Received(1).SendAsync(
            Arg.Is<SendContext>(c => c.Repeat == 0 && c.Delay == 0 && c.IsolateFromMessageState),
            Arg.Any<IProgress<Result<bool>>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Framework_ItemsComeFromTheCatalog_AndUnsupportedOnesAreDisabled()
    {
        var cut = Render<MessagingPanel>();

        await SelectProviderAndEndpointAsync(cut);

        var items = cut.FindComponents<MudSelectItem<string>>()
            .Where(i => i.Instance.Value is "MassTransit" or "Rebus").ToList();
        items.Should().HaveCount(2);
        items.Single(i => i.Instance.Value == "Rebus").Instance.Disabled.Should().BeTrue();
        await _catalog.Received().GetFrameworksAsync("http://127.0.0.1:15672", Arg.Any<int>());
    }

    [Fact]
    public async Task Framework_SelectionClears_WhenTheCatalogNoLongerListsIt()
    {
        var initialFrameworks = new List<FrameworkDescriptor>
        {
            new("MassTransit", []),
            new("Rebus", [], Supported: false, UnsupportedReason: "Rebus needs transport headers; RabbitMq cannot carry them."),
        };
        var frameworksWithoutRebus = new List<FrameworkDescriptor> { new("MassTransit", []) };
        _catalog.GetFrameworksAsync(Arg.Any<string?>(), Arg.Any<int>()).Returns(initialFrameworks, frameworksWithoutRebus);

        var cut = Render<MessagingPanel>();

        await SelectProviderAndEndpointAsync(cut);

        var frameworkSelect = cut.FindComponent<MudSelect<string>>();
        await cut.InvokeAsync(() => frameworkSelect.Instance.ValueChanged.InvokeAsync("Rebus"));

        var provider = new Provider { Name = "RabbitMq", Address = "http://127.0.0.1:15672" };
        await cut.InvokeAsync(() => cut.FindComponent<ProviderSelector>().Instance.ValueChanged.InvokeAsync(provider));

        cut.FindComponent<MudSelect<string>>().Instance.Value.Should().BeNull();
    }

    [Fact]
    public async Task Send_Failure_ShowsTheResultMessageInTheSnackbar()
    {
        _orchestrator
            .SendAsync(Arg.Any<SendContext>(), Arg.Any<IProgress<Result<bool>>?>(), Arg.Any<CancellationToken>())
            .Returns(new Failure<bool>("Assembly name is required for EasyNetQ."));

        var cut = Render<MessagingPanel>();

        await SelectProviderAndEndpointAsync(cut);

        await cut.Find("textarea").InputAsync(new() { Value = "{\"orderId\":\"1\"}" });
        await cut.Find("button.messaging-panel-send").ClickAsync(new());

        _snackbar.Received(1).Add(
            "Assembly name is required for EasyNetQ.",
            Severity.Error,
            Arg.Any<Action<SnackbarOptions>?>(),
            Arg.Any<string?>());
    }

    private static async Task SelectProviderAndEndpointAsync(IRenderedComponent<MessagingPanel> cut)
    {
        var provider = new Provider { Name = "RabbitMq", Address = "http://127.0.0.1:15672" };
        var endpoint = new EndpointDetails { Name = "orders", Address = "http://127.0.0.1:15672", Provider = "RabbitMq", Type = "Queue" };

        await cut.InvokeAsync(() => cut.FindComponent<ProviderSelector>().Instance.ValueChanged.InvokeAsync(provider));
        await cut.InvokeAsync(() => cut.FindComponent<EndpointSelector>().Instance.ValueChanged.InvokeAsync(endpoint));
    }
}
