using Bunit;
using FluentAssertions;
using Iris.Components.Sagas;
using Iris.Sagas;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;

namespace Iris.Components.Test.Sagas;

public class UnmatchedTransitionsDialogTests : IrisTestContext
{
    public UnmatchedTransitionsDialogTests()
    {
        Services.AddMessageBus();
        Services.AddSingleton(Substitute.For<ISagaDefinitionProvider>());
        Services.AddSingleton<ISagaSpanMapper, StubSpanMapper>();
        Services.AddSingleton<SagaDefinitionState>();
        Services.AddSingleton<SagaInstanceState>();
    }

    // MudDialog renders through the provider rather than in place, so the dialog only produces markup
    // when it is shown the way the chip shows it.
    private async Task<IRenderedComponent<MudDialogProvider>> ShowDialogAsync()
    {
        var provider = RenderComponent<MudDialogProvider>();
        var dialogs = Services.GetRequiredService<IDialogService>();
        await provider.InvokeAsync(() => dialogs.ShowAsync(typeof(UnmatchedTransitionsDialog), "Unmatched transitions"));
        return provider;
    }

    [Fact(DisplayName = "Lists an unmatched transition with its saga id, states and type hint")]
    public async Task Lists_An_Unmatched_Transition()
    {
        var instances = Services.GetRequiredService<SagaInstanceState>();
        var sagaId = Guid.NewGuid();
        await instances.IngestAsync([StubSpanMapper.Span(sagaId, "Nowhere", "Elsewhere", hint: "GhostStateMachine")]);

        var provider = await ShowDialogAsync();

        provider.Markup.Should().Contain(sagaId.ToString());
        provider.Markup.Should().Contain("Nowhere").And.Contain("Elsewhere").And.Contain("GhostStateMachine");
    }

    [Fact(DisplayName = "Says the table holds only the most recent ones when more went unmatched than it keeps")]
    public async Task Admits_The_Sample_Is_Bounded()
    {
        var instances = Services.GetRequiredService<SagaInstanceState>();
        var overflow = SagaInstanceState.MaxUnmatched + 5;
        await instances.IngestAsync(Enumerable
            .Range(0, overflow)
            .Select(_ => StubSpanMapper.Span(Guid.NewGuid(), "Nowhere", "Elsewhere"))
            .ToList());

        var provider = await ShowDialogAsync();

        provider.Markup.Should().Contain($"The {SagaInstanceState.MaxUnmatched} most recent of {overflow} unmatched transitions");
    }
}
