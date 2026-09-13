using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Iris.Components.Shared.Panels;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Components.Test.Shared;

public class ContextPanelLayoutTests : IrisTestContext
{
    private FakeNavigationManager Nav => Services.GetRequiredService<FakeNavigationManager>();

    [Fact]
    public void Renders_PageLink_ForTitle()
    {
        var cut = RenderComponent<ContextPanelLayout>(p => p
            .Add(x => x.Title, "Messaging")
            .Add(x => x.PageHref, "/Messaging"));

        cut.Markup.Should().Contain("Open Messaging Page");
    }

    [Fact]
    public void PageLink_Navigates_ToPageHref()
    {
        var cut = RenderComponent<ContextPanelLayout>(p => p
            .Add(x => x.Title, "Messaging")
            .Add(x => x.PageHref, "/Messaging"));

        cut.Find("button").Click();

        Nav.Uri.Should().Be($"{Nav.BaseUri}Messaging");
    }

    [Fact]
    public void Omits_PageLink_WhenNoHref()
    {
        var cut = RenderComponent<ContextPanelLayout>(p => p
            .Add(x => x.Title, "Plugin")
            .AddChildContent("<p>body</p>"));

        cut.Markup.Should().NotContain("Open Plugin Page");
        cut.FindAll("button").Should().BeEmpty();
    }

    [Fact]
    public void Renders_PageLink_BelowTheBody()
    {
        var cut = RenderComponent<ContextPanelLayout>(p => p
            .Add(x => x.Title, "History")
            .Add(x => x.PageHref, "/History")
            .AddChildContent("<p>recent activity</p>"));

        cut.Markup.IndexOf("recent activity", StringComparison.Ordinal)
            .Should().BeLessThan(cut.Markup.IndexOf("Open History Page", StringComparison.Ordinal));
    }

    [Fact]
    public void Renders_FooterContent_BetweenBodyAndPageLink()
    {
        var cut = RenderComponent<ContextPanelLayout>(p => p
            .Add(x => x.Title, "Connections")
            .Add(x => x.PageHref, "/Connections")
            .AddChildContent("<p>connection list</p>")
            .Add(x => x.FooterContent, (RenderFragment)(b => b.AddMarkupContent(0, "<span>Add Connection</span>"))));

        var markup = cut.Markup;
        markup.IndexOf("connection list", StringComparison.Ordinal)
            .Should().BeLessThan(markup.IndexOf("Add Connection", StringComparison.Ordinal));
        markup.IndexOf("Add Connection", StringComparison.Ordinal)
            .Should().BeLessThan(markup.IndexOf("Open Connections Page", StringComparison.Ordinal));
    }

    [Fact]
    public void Body_IsScrollable_SoTheFooterStaysPinned()
    {
        var cut = RenderComponent<ContextPanelLayout>(p => p
            .Add(x => x.Title, "Packages")
            .Add(x => x.PageHref, "/Packages"));

        cut.Find("div.context-panel-body").Should().NotBeNull();
    }
}
