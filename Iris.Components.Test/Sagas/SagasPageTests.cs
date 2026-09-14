using System.Runtime.Loader;
using AngleSharp.Dom;
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
    private const string ShipType = "Sample.ShipStateMachine";
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

        Render<MudPopoverProvider>();
    }

    private async Task<LoadedAssembly> LoadOrderGraphAsync(bool includeBroken = false)
    {
        var loaded = new LoadedAssembly { Assembly = typeof(SagasPageTests).Assembly, Context = AssemblyLoadContext.Default };
        var graph = new SagaGraph(OrderType, "OrderStateMachine", "MassTransit",
            [
                new SagaState("Initial", true, false),
                new SagaState("Submitted", false, false),
                new SagaState("Accepted", false, true),
            ],
            [
                new SagaTransitionDefinition("Initial", "Submitted", "SubmitOrder"),
                new SagaTransitionDefinition("Submitted", "Accepted", "AcceptOrder"),
            ]);
        var results = new List<SagaDefinitionResult> { new(graph, OrderType, null) };
        if (includeBroken)
            results.Add(new SagaDefinitionResult(null, "Sample.BrokenMachine", "No parameterless constructor. Iris cannot supply: IPricing pricing"));
        _provider.Discover(loaded).Returns(results);
        await Services.GetRequiredService<SagaDefinitionState>().AddAssemblyAsync(loaded);
        return loaded;
    }

    /// <summary>A second graph in a second assembly, so unloading the first still leaves the page a list to show.</summary>
    private async Task LoadShipGraphAsync()
    {
        var loaded = new LoadedAssembly { Assembly = typeof(SagaGraph).Assembly, Context = AssemblyLoadContext.Default };
        var graph = new SagaGraph(ShipType, "ShipStateMachine", "MassTransit",
            [new SagaState("Ready", true, false), new SagaState("Shipped", false, true)],
            [new SagaTransitionDefinition("Ready", "Shipped", "ShipOrder")]);
        _provider.Discover(loaded).Returns(new List<SagaDefinitionResult> { new(graph, ShipType, null) });
        await Services.GetRequiredService<SagaDefinitionState>().AddAssemblyAsync(loaded);
    }

    private static IElement DefinitionRow(IRenderedComponent<SagasPage> cut, string displayName)
        => cut.FindAll(".saga-definition-item").Single(e => e.TextContent.Contains(displayName));

    [Fact(DisplayName = "Empty state explains that a loaded assembly with a state machine is needed")]
    public void Empty_State()
    {
        var cut = Render<SagasPage>();

        cut.Markup.Should().Contain("No state machines found");
        cut.Markup.Should().Contain("MassTransitStateMachine");
    }

    [Fact(DisplayName = "Lists usable definitions and shows failed ones with their reason")]
    public async Task Lists_Definitions()
    {
        await LoadOrderGraphAsync(includeBroken: true);

        var cut = Render<SagasPage>();

        cut.Markup.Should().Contain("OrderStateMachine");
        cut.Markup.Should().Contain("BrokenMachine");
        cut.Markup.Should().Contain("IPricing pricing");
        cut.FindAll(".saga-definition-item.saga-definition-error").Should().HaveCount(1);
    }

    [Fact(DisplayName = "Selecting a definition renders its graph and instance panel")]
    public async Task Select_Definition_Renders_Graph()
    {
        await LoadOrderGraphAsync();
        var cut = Render<SagasPage>();

        await cut.Find(".saga-definition-item").ClickAsync(new MouseEventArgs());

        cut.FindAll("svg.saga-graph").Should().HaveCount(1);
        cut.Markup.Should().Contain("No instances yet");
    }

    /// <summary>
    /// The assembly comes back at the end because that is the only way the stale selection is visible:
    /// while the graph is missing the page cannot render it either way, so a selection that was never
    /// cleared would only reveal itself when the same type name resolves again.
    /// </summary>
    [Fact(DisplayName = "Unloading the selected definition clears the definition and instance selection")]
    public async Task Unloading_Selected_Definition_Clears_Selection()
    {
        var orderAssembly = await LoadOrderGraphAsync();
        await LoadShipGraphAsync();
        var cut = Render<SagasPage>();
        await DefinitionRow(cut, "OrderStateMachine").ClickAsync(new MouseEventArgs());
        await Services.GetRequiredService<SagaInstanceState>().IngestAsync([StubSpanMapper.Span(Guid.NewGuid(), "Initial", "Submitted")]);
        cut.WaitForAssertion(() => cut.FindAll(".saga-instance-item").Should().HaveCount(1));
        await cut.Find(".saga-instance-item").ClickAsync(new MouseEventArgs());
        cut.FindAll("svg.saga-graph").Should().HaveCount(1);

        await Services.GetRequiredService<SagaDefinitionState>().RemoveAssemblyAsync(orderAssembly.Assembly.FullName!);
        cut.WaitForAssertion(() => cut.FindAll("svg.saga-graph").Should().BeEmpty());

        await Services.GetRequiredService<SagaDefinitionState>().AddAssemblyAsync(orderAssembly);

        cut.WaitForAssertion(() => DefinitionRow(cut, "OrderStateMachine").Should().NotBeNull());
        cut.Markup.Should().Contain("Select a state machine to see its graph.");
        cut.FindAll("svg.saga-graph").Should().BeEmpty();
        cut.FindAll(".saga-instance-item").Should().BeEmpty();
        cut.FindAll(".saga-definition-item[aria-current='true']").Should().BeEmpty();
    }

    [Fact(DisplayName = "Pressing Enter on a definition row selects it")]
    public async Task Enter_Selects_Definition()
    {
        await LoadOrderGraphAsync();
        var cut = Render<SagasPage>();

        await DefinitionRow(cut, "OrderStateMachine").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        cut.FindAll("svg.saga-graph").Should().HaveCount(1);
        DefinitionRow(cut, "OrderStateMachine").GetAttribute("aria-current").Should().Be("true");
    }

    [Fact(DisplayName = "Timeline lists the newest transition first")]
    public async Task Timeline_Lists_Newest_First()
    {
        await LoadOrderGraphAsync();
        var cut = Render<SagasPage>();
        await DefinitionRow(cut, "OrderStateMachine").ClickAsync(new MouseEventArgs());
        var sagaId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow;

        await Services.GetRequiredService<SagaInstanceState>().IngestAsync(
        [
            StubSpanMapper.Span(sagaId, "Initial", "Submitted", at: start),
            StubSpanMapper.Span(sagaId, "Submitted", "Accepted", at: start.AddSeconds(1)),
        ]);
        cut.WaitForAssertion(() => cut.FindAll(".saga-instance-item").Should().HaveCount(1));
        await cut.Find(".saga-instance-item").ClickAsync(new MouseEventArgs());

        var rows = cut.FindAll(".saga-timeline-row");
        rows.Should().HaveCount(2);
        rows[0].TextContent.Should().Contain("Submitted to Accepted");
        rows[1].TextContent.Should().Contain("Initial to Submitted");
    }

    [Fact(DisplayName = "Instances appear after spans arrive and selecting one highlights the graph")]
    public async Task Instances_Appear_And_Highlight()
    {
        await LoadOrderGraphAsync();
        var cut = Render<SagasPage>();
        await cut.Find(".saga-definition-item").ClickAsync(new MouseEventArgs());
        var sagaId = Guid.NewGuid();

        await Services.GetRequiredService<SagaInstanceState>().IngestAsync([StubSpanMapper.Span(sagaId, "Initial", "Submitted")]);
        cut.WaitForAssertion(() => cut.FindAll(".saga-instance-item").Should().HaveCount(1));
        await cut.Find(".saga-instance-item").ClickAsync(new MouseEventArgs());

        cut.Find("g.node[data-state='Submitted']").ClassList.Should().Contain("node-current");
        cut.Markup.Should().Contain(sagaId.ToString("N")[..8]);
    }
}
