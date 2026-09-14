using Bunit;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Components.Endpoints;
using Iris.Components.Messaging;
using Iris.Contracts.Brokers.Models;
using Iris.Contracts.Messaging.Frameworks;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;

namespace Iris.Components.Test.Shared;

/// <summary>
/// Renders panels through their own markup, which is what catches an unresolved
/// ContextPanelLayout. Razor downgrades an unknown component to a literal element
/// with only a warning, so the children still render and only the footer goes missing.
/// </summary>
public class ContextPanelFooterTests : IrisTestContext
{
    private void RegisterMessagingPanelDependencies()
    {
        var broker = Substitute.For<IBrokerService>();
        broker.GetProvidersAsync().Returns(Task.FromResult(new List<Provider>()));
        broker.GetEndpointsAsync().Returns(Task.FromResult(new List<EndpointDetails>()));
        Services.AddSingleton(broker);
        Services.AddSingleton(Substitute.For<IMessageSendOrchestrator>());

        var catalog = Substitute.For<IFrameworkCatalog>();
        catalog.GetFrameworksAsync(Arg.Any<string?>(), Arg.Any<int>())
            .Returns(new List<FrameworkDescriptor>());
        Services.AddSingleton(catalog);
        Services.AddSingleton(Substitute.For<IMessageBus>());
        Services.AddSingleton<MessagingSettings>();
        Services.AddScoped<MessageState>();

        // MessagingPanel's selectors are MudSelect, which registers a popover on
        // init and throws if none is mounted.
        JSInterop.Setup<int>("mudpopoverHelper.countProviders", _ => true);
        Render<MudPopoverProvider>();
    }

    [Fact]
    public void MessagingPanel_RendersPageLink()
    {
        RegisterMessagingPanelDependencies();

        var cut = Render<MessagingPanel>();

        cut.Markup.Should().Contain("Open Messaging Page");
    }

    [Fact]
    public void EndpointsPanel_RendersPageLink()
    {
        var broker = Substitute.For<IBrokerService>();
        broker.GetEndpointsAsync().Returns(Task.FromResult(new List<EndpointDetails>()));
        Services.AddSingleton(broker);

        var cut = Render<EndpointsPanel>();

        cut.Markup.Should().Contain("Open Endpoints Page");
    }

    [Fact]
    public void MessagingPanel_RendersLayoutShell()
    {
        RegisterMessagingPanelDependencies();

        var cut = Render<MessagingPanel>();

        cut.FindAll("div.context-panel-layout").Should().ContainSingle();
        cut.FindAll("div.context-panel-body").Should().ContainSingle();
    }
}
