using Bunit;
using FluentAssertions;
using Iris.Components.Shared.Toolbar;
using Xunit;

namespace Iris.Components.Test.Shared;

public class IrisPageHeaderTests : IrisTestContext
{
    [Fact]
    public void Renders_Title()
    {
        var cut = RenderComponent<IrisPageHeader>(p => p
            .Add(x => x.Title, "Endpoints"));

        cut.Markup.Should().Contain("Endpoints");
    }

    [Fact]
    public void Renders_Icon_WhenSupplied()
    {
        var cut = RenderComponent<IrisPageHeader>(p => p
            .Add(x => x.Title, "Endpoints")
            .Add(x => x.Icon, Iris.Components.Theme.IrisIcons.Endpoints));

        cut.FindAll(".iris-page-header-icon").Should().NotBeEmpty();
    }

    [Fact]
    public void Omits_Icon_WhenNotSupplied()
    {
        var cut = RenderComponent<IrisPageHeader>(p => p
            .Add(x => x.Title, "Endpoints"));

        cut.FindAll(".iris-page-header-icon").Should().BeEmpty();
    }

    [Fact]
    public void Renders_StatusAndActions_InTheirOwnSlots()
    {
        var cut = RenderComponent<IrisPageHeader>(p => p
            .Add(x => x.Title, "Endpoints")
            .Add(x => x.Status, "<span>215 endpoints</span>")
            .Add(x => x.Actions, "<button>Refresh</button>"));

        cut.Find(".iris-page-header-status").TextContent.Should().Contain("215 endpoints");
        cut.Find(".iris-page-header-actions").TextContent.Should().Contain("Refresh");
    }

    [Fact]
    public void Renders_Actions_AfterStatus()
    {
        var cut = RenderComponent<IrisPageHeader>(p => p
            .Add(x => x.Title, "Endpoints")
            .Add(x => x.Status, "<span>status-slot</span>")
            .Add(x => x.Actions, "<span>actions-slot</span>"));

        cut.Markup.IndexOf("status-slot", StringComparison.Ordinal)
            .Should().BeLessThan(cut.Markup.IndexOf("actions-slot", StringComparison.Ordinal));
    }
}
