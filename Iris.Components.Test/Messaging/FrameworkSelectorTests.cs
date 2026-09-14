using Bunit;
using FluentAssertions;
using Iris.Components.Messaging;
using Iris.Contracts.Messaging.Frameworks;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;

namespace Iris.Components.Test.Messaging;

public class FrameworkSelectorTests : IrisTestContext
{
    private readonly MessageState _state = new(new MessagingSettings(), Substitute.For<IMessageBus>());

    // MudSelect renders its popover content through MudPopoverProvider rather than
    // inline under the select itself, so component lookups that need real markup
    // (tooltips included) have to search this fragment, not the FrameworkSelector one.
    private readonly IRenderedComponent<MudPopoverProvider> _popoverProvider;

    public FrameworkSelectorTests()
    {
        Services.AddSingleton(_state);
        JSInterop.Setup<int>("mudpopoverHelper.countProviders", _ => true);
        _popoverProvider = Render<MudPopoverProvider>();

        _state.SetAvailableFrameworks(
        [
            new FrameworkDescriptor("MassTransit", []),
            new FrameworkDescriptor("Rebus", [], Supported: false, UnsupportedReason: "Rebus needs transport headers; Azure cannot carry them."),
            new FrameworkDescriptor("Brighter", [], Supported: true, DroppedKeys: ["cloudEvents_time", "cloudEvents_source"]),
            new FrameworkDescriptor("Wolverine", [], Supported: true, Verified: false),
        ]);
    }

    [Fact(DisplayName = "Lists every framework from the catalog, disabling unsupported ones")]
    public async Task Renders_CatalogItems()
    {
        var cut = Render<FrameworkSelector>();

        await cut.Find(".mud-input-control").MouseDownAsync(new());

        var items = cut.FindComponents<MudSelectItem<string>>();
        items.Select(i => i.Instance.Value).Should().Equal("MassTransit", "Rebus", "Brighter", "Wolverine");
        items.Single(i => i.Instance.Value == "Rebus").Instance.Disabled.Should().BeTrue();
        items.Single(i => i.Instance.Value == "MassTransit").Instance.Disabled.Should().BeFalse();
    }

    [Fact(DisplayName = "Unsupported and partially carried frameworks explain themselves in a tooltip")]
    public async Task Renders_Reasons()
    {
        var cut = Render<FrameworkSelector>();

        await cut.Find(".mud-input-control").MouseDownAsync(new());

        var tooltips = _popoverProvider.FindComponents<MudTooltip>().Select(t => t.Instance.Text).ToList();
        tooltips.Should().Contain("Rebus needs transport headers; Azure cannot carry them.");
        tooltips.Should().Contain(t => t != null && t.Contains("2 optional headers not sent"));
    }

    [Fact(DisplayName = "An unverified pairing stays selectable and says so in its tooltip")]
    public async Task Renders_UnverifiedNotice()
    {
        var cut = Render<FrameworkSelector>();

        await cut.Find(".mud-input-control").MouseDownAsync(new());

        var tooltips = _popoverProvider.FindComponents<MudTooltip>().Select(t => t.Instance.Text).ToList();
        tooltips.Should().Contain(t => t != null && t.StartsWith("Not verified"));
        cut.FindComponents<MudSelectItem<string>>()
            .Single(i => i.Instance.Value == "Wolverine").Instance.Disabled.Should().BeFalse();
    }

    [Fact(DisplayName = "Choosing a framework selects its descriptor on the state")]
    public async Task Selecting_SetsDescriptor()
    {
        var cut = Render<FrameworkSelector>();

        var select = cut.FindComponent<MudSelect<string>>();
        await cut.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync("MassTransit"));

        _state.SelectedFrameworkDescriptor.Should().NotBeNull();
        _state.SelectedFrameworkDescriptor!.Name.Should().Be("MassTransit");
    }

    [Fact(DisplayName = "A cleared selection shows the notice as helper text")]
    public void Notice_IsShown()
    {
        _state.SetFramework(_state.AvailableFrameworks[0]);
        _state.SetAvailableFrameworks([new FrameworkDescriptor("MassTransit", [], Supported: false, UnsupportedReason: "gone")]);

        var cut = Render<FrameworkSelector>();

        cut.Markup.Should().Contain("gone");
    }
    [Fact(DisplayName = "Framework-declared property keys render as text; hand-added keys stay editable")]
    public void PropertyKeys_LockedForFrameworkRows()
    {
        var easyNetQ = new FrameworkDescriptor("EasyNetQ",
        [
            new FrameworkInput("TypeName", "Type name", "the fully qualified type"),
        ]);
        _state.SetAvailableFrameworks([easyNetQ]);
        _state.SetFramework(easyNetQ);
        _state.AddAdditionalProperty(new DictionaryViewModel { Key = "mine", Value = "1" });

        var cut = Render<FrameworkSelector>();

        cut.FindAll("tbody tr").Count.Should().Be(2);

        // The declared row shows its key as text, so only its value is an input.
        cut.FindAll("tbody tr")[0].TextContent.Should().Contain("TypeName");
        cut.FindAll("tbody tr")[0].QuerySelectorAll("input").Length.Should().Be(1);

        // The hand-added row keeps an editable key alongside its value.
        cut.FindAll("tbody tr")[1].QuerySelectorAll("input").Length.Should().Be(2);
    }
}
