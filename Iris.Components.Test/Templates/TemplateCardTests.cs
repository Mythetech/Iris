using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Iris.Components.Templates;
using Iris.Contracts.Templates.Models;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Xunit;

namespace Iris.Components.Test.Templates;

public class TemplateCardTests : IrisTestContext
{
    private readonly IRenderedComponent<MudPopoverProvider> _popoverProvider;

    public TemplateCardTests()
    {
        // MudMenu (the overflow action) and MudTooltip both render through the popover
        // service, which needs a provider in the tree. Captured so tests can inspect
        // where portaled content actually ends up, not just that this exists.
        _popoverProvider = Render<MudPopoverProvider>();
    }

    // Template.TemplateId is a Guid, not a string.
    private static Template BuildTemplate() => new()
    {
        TemplateId = Guid.NewGuid(),
        Name = "HermesCrashContext",
    };

    private static IElement ByLabel(IReadOnlyList<IElement> elements, string label) =>
        elements.Single(e => e.GetAttribute("aria-label") == label);

    [Fact]
    public void Renders_ExactlySevenInlineActions_AndTheOverflowTrigger()
    {
        var cut = Render<TemplateCard>(p => p
            .Add(x => x.Template, BuildTemplate()));

        // Pins the design's core premise: every action is always in the DOM, at every
        // width. CSS decides visibility later; nothing here should ever be conditionally
        // rendered based on size.
        cut.FindAll(".template-card-inline button").Should().HaveCount(7);
        cut.FindAll(".template-card-overflow").Should().HaveCount(1);
    }

    [Fact]
    public void Renders_PreviewAndEdit_AsTheFirstTwoInlineActions()
    {
        var cut = Render<TemplateCard>(p => p
            .Add(x => x.Template, BuildTemplate()));

        var inline = cut.FindAll(".template-card-inline button");

        inline[0].GetAttribute("aria-label").Should().Be("Preview");
        inline[1].GetAttribute("aria-label").Should().Be("Edit");
        inline[2].GetAttribute("aria-label").Should().Be("Copy JSON");
    }

    [Fact]
    public void InlineActions_CarryTheTierMarkerClasses_TheCssDependsOn()
    {
        var cut = Render<TemplateCard>(p => p
            .Add(x => x.Template, BuildTemplate()));

        var inline = cut.FindAll(".template-card-inline button");

        // tier-copy and tier-wide are the only point of contact between this markup and
        // TemplateCard.razor.css. A typo on either name would pass every other assertion
        // in this file while silently breaking a tier, so pin them explicitly.
        ByLabel(inline, "Preview").ClassList.Should().NotContain("tier-copy").And.NotContain("tier-wide");
        ByLabel(inline, "Edit").ClassList.Should().NotContain("tier-copy").And.NotContain("tier-wide");
        ByLabel(inline, "Copy JSON").ClassList.Should().Contain("tier-copy");
        ByLabel(inline, "Duplicate").ClassList.Should().Contain("tier-wide");
        ByLabel(inline, "Export").ClassList.Should().Contain("tier-wide");
        ByLabel(inline, "Rename").ClassList.Should().Contain("tier-wide");
        ByLabel(inline, "Delete").ClassList.Should().Contain("tier-wide");
    }

    [Fact]
    public async Task Edit_NavigatesToTheTemplateEditor()
    {
        var template = BuildTemplate();

        var cut = Render<TemplateCard>(p => p
            .Add(x => x.Template, template));

        await cut.Find("button[aria-label=\"Edit\"]").ClickAsync(new());

        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.Uri.Should().Be($"{nav.BaseUri}Templates/{template.TemplateId}");
    }

    [Fact]
    public async Task Preview_RaisesOnPreview()
    {
        Template? previewed = null;

        var cut = Render<TemplateCard>(p => p
            .Add(x => x.Template, BuildTemplate())
            .Add(x => x.OnPreview, t => previewed = t));

        await cut.Find("button[aria-label=\"Preview\"]").ClickAsync(new());

        previewed.Should().NotBeNull();
    }

    [Fact]
    public async Task OverflowMenu_PortalsItsFiveItems_OutsideTheCardsOwnMarkup()
    {
        var cut = Render<TemplateCard>(p => p
            .Add(x => x.Template, BuildTemplate()));

        await cut.Find(".template-card-overflow button").ClickAsync(new());

        // MudMenu renders its items through MudPopover into the shared MudPopoverProvider,
        // not as children of .template-card-overflow. This is why no ::deep rule scoped to
        // the card can ever hide an individual menu item: the five items exist somewhere in
        // the rendered output (in the provider), but never as descendants of the trigger.
        _popoverProvider.FindAll(".mud-menu-item").Should().HaveCount(5);
        cut.Find(".template-card-overflow").QuerySelectorAll(".tier-copy-menu").Should().BeEmpty();
    }
}
