using Bunit;
using FluentAssertions;
using Iris.Components.Messaging;
using Iris.Components.Templates;
using Iris.Contracts.Templates.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Iris.Components.Test.Messaging;

public class TemplateTabListTests : IrisTestContext
{
    private readonly ITemplateService _templates = Substitute.For<ITemplateService>();
    private readonly TemplatesState _state;

    public TemplateTabListTests()
    {
        _templates.GetTemplatesAsync().Returns(_ => new List<Template>
        {
            new() { TemplateId = Guid.NewGuid(), Name = "First", Json = "{}" },
        });

        _state = new TemplatesState(_templates, NullLogger<TemplatesState>.Instance);

        Services.AddSingleton(_state);
        Services.AddSingleton(new LayoutState(Substitute.For<IMessagingLayoutService>()));
    }

    private IRenderedComponent<TemplateTabList> RenderTabList()
    {
        AddPopoverProvider();

        var instance = new TemplateTabList();
        return Render<TemplateTabList>(p => p.Add(x => x.Instance, instance));
    }

    [Fact(DisplayName = "Disposing the tab leaves no handler behind on TemplatesState")]
    public async Task Unsubscribes_on_dispose()
    {
        // The old subscription used one async lambda to subscribe and a second,
        // syntactically identical but distinct one to unsubscribe, so the -= matched
        // nothing. TemplatesState is scoped, which in Blazor Hybrid means application
        // lifetime, so a handler survived every activation of the Templates tab and each
        // survivor re-ran the tab's whole initialization on the next change.
        RenderTabList();

        // DisposeComponentsAsync, not cut.Dispose: only unmounting makes Blazor call
        // IDisposable.Dispose on the component.
        await DisposeComponentsAsync();

        _state.TemplateStateChanged.Should().BeNull(
            "the handler added in OnInitialized must be the one removed in Dispose");
    }

    [Fact(DisplayName = "Repeated activations do not accumulate handlers")]
    public async Task Does_not_accumulate_handlers_across_activations()
    {
        for (var i = 0; i < 3; i++)
        {
            RenderTabList();
            await DisposeComponentsAsync();
        }

        _state.TemplateStateChanged.Should().BeNull();
    }

    [Fact(DisplayName = "A template change refreshes the tab badge")]
    public async Task Refreshes_the_badge_when_templates_change()
    {
        AddPopoverProvider();

        var instance = new TemplateTabList();
        var cut = Render<TemplateTabList>(p => p.Add(x => x.Instance, instance));

        instance.BadgeCount.Should().Be(1);

        await cut.InvokeAsync(() => _state.CreateTemplateAsync(
            new Template { TemplateId = Guid.NewGuid(), Name = "Second", Json = "{}" }));

        instance.BadgeCount.Should().Be(2);
    }
}
