using System.Runtime.Loader;
using Bunit;
using FluentAssertions;
using Iris.Assemblies;
using Iris.Components.Sagas;
using Iris.Sagas;
using Iris.Telemetry;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;
using Mythetech.Framework.Infrastructure.Settings;
using NSubstitute;

namespace Iris.Components.Test.Sagas;

public class SagasPageTests : IrisTestContext
{
    private const string OrderType = "Sample.OrderStateMachine";
    private readonly ISagaDefinitionProvider _provider = Substitute.For<ISagaDefinitionProvider>();
    private readonly IOtlpReceiver _receiver = Substitute.For<IOtlpReceiver>();

    public SagasPageTests()
    {
        Services.AddMessageBus();
        Services.AddSingleton(_provider);
        Services.AddSingleton<ISagaSpanMapper, StubSpanMapper>();
        Services.AddSingleton<SagaDefinitionState>();
        Services.AddSingleton<SagaInstanceState>();
        Services.AddSingleton(_receiver);
        Services.AddSingleton(Substitute.For<ISettingsProvider>());
        Services.AddSingleton(new SagaTelemetrySettings());
        _receiver.Status.Returns(OtlpReceiverStatus.Stopped);

        RenderComponent<MudPopoverProvider>();
    }

    private async Task LoadOrderGraphAsync(bool includeBroken = false)
    {
        var loaded = new LoadedAssembly { Assembly = typeof(SagasPageTests).Assembly, Context = AssemblyLoadContext.Default };
        var graph = new SagaGraph(OrderType, "OrderStateMachine", "MassTransit",
            [new SagaState("Initial", true, false), new SagaState("Submitted", false, false)],
            [new SagaTransitionDefinition("Initial", "Submitted", "SubmitOrder")]);
        var results = new List<SagaDefinitionResult> { new(graph, OrderType, null) };
        if (includeBroken)
            results.Add(new SagaDefinitionResult(null, "Sample.BrokenMachine", "No parameterless constructor. Iris cannot supply: IPricing pricing"));
        _provider.Discover(loaded).Returns(results);
        await Services.GetRequiredService<SagaDefinitionState>().AddAssemblyAsync(loaded);
    }

    [Fact(DisplayName = "Empty state explains that a loaded assembly with a state machine is needed")]
    public void Empty_State()
    {
        var cut = RenderComponent<SagasPage>();

        cut.Markup.Should().Contain("No state machines found");
        cut.Markup.Should().Contain("MassTransitStateMachine");
    }

    [Fact(DisplayName = "Lists usable definitions and shows failed ones with their reason")]
    public async Task Lists_Definitions()
    {
        await LoadOrderGraphAsync(includeBroken: true);

        var cut = RenderComponent<SagasPage>();

        cut.Markup.Should().Contain("OrderStateMachine");
        cut.Markup.Should().Contain("BrokenMachine");
        cut.Markup.Should().Contain("IPricing pricing");
        cut.FindAll(".saga-definition-item.saga-definition-error").Should().HaveCount(1);
    }

    [Fact(DisplayName = "Selecting a definition renders its graph and instance panel")]
    public async Task Select_Definition_Renders_Graph()
    {
        await LoadOrderGraphAsync();
        var cut = RenderComponent<SagasPage>();

        await cut.Find(".saga-definition-item").ClickAsync(new MouseEventArgs());

        cut.FindAll("svg.saga-graph").Should().HaveCount(1);
        cut.Markup.Should().Contain("No instances yet");
    }

    [Fact(DisplayName = "Instances appear after spans arrive and selecting one highlights the graph")]
    public async Task Instances_Appear_And_Highlight()
    {
        await LoadOrderGraphAsync();
        var cut = RenderComponent<SagasPage>();
        await cut.Find(".saga-definition-item").ClickAsync(new MouseEventArgs());
        var sagaId = Guid.NewGuid();

        await Services.GetRequiredService<SagaInstanceState>().IngestAsync([StubSpanMapper.Span(sagaId, "Initial", "Submitted")]);
        cut.WaitForAssertion(() => cut.FindAll(".saga-instance-item").Should().HaveCount(1));
        await cut.Find(".saga-instance-item").ClickAsync(new MouseEventArgs());

        cut.Find("g.node[data-state='Submitted']").ClassList.Should().Contain("node-current");
        cut.Markup.Should().Contain(sagaId.ToString("N")[..8]);
    }
}
