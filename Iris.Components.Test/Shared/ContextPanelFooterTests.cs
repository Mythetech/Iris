using Bunit;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Components.Endpoints;
using Iris.Components.Messaging;
using Iris.Contracts.Brokers.Models;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Iris.Components.Test.Shared;

/// <summary>
/// Renders panels through their own markup, which is what catches an unresolved
/// ContextPanelLayout. Razor downgrades an unknown component to a literal element
/// with only a warning, so the children still render and only the footer goes missing.
/// </summary>
public class ContextPanelFooterTests : IrisTestContext
{
    [Fact]
    public void MessagingPanel_RendersPageLink()
    {
        var cut = RenderComponent<MessagingPanel>();

        cut.Markup.Should().Contain("Open Messaging Page");
    }

    [Fact]
    public void EndpointsPanel_RendersPageLink()
    {
        var broker = Substitute.For<IBrokerService>();
        broker.GetEndpointsAsync().Returns(Task.FromResult(new List<EndpointDetails>()));
        Services.AddSingleton(broker);

        var cut = RenderComponent<EndpointsPanel>();

        cut.Markup.Should().Contain("Open Endpoints Page");
    }

    [Fact]
    public void MessagingPanel_RendersLayoutShell()
    {
        var cut = RenderComponent<MessagingPanel>();

        cut.FindAll("div.context-panel-layout").Should().ContainSingle();
        cut.FindAll("div.context-panel-body").Should().ContainSingle();
    }
}
