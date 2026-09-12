using System.Runtime.Loader;
using FluentAssertions;
using Iris.Assemblies;
using Iris.Components.Sagas;
using Iris.Components.Sagas.Messages;
using Iris.Sagas;
using Iris.Telemetry;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;

namespace Iris.Components.Test.Sagas;

public class SagaInstanceStateTests
{
    private const string OrderType = "Sample.OrderStateMachine";
    private const string PaymentType = "Sample.PaymentStateMachine";

    private static SagaGraph Graph(string typeName, params string[] states) => new(typeName, typeName.Split('.').Last(), "Stub",
        states.Select(s => new SagaState(s, s == "Initial", false)).ToList(), []);

    private static async Task<(SagaInstanceState Instances, SagaDefinitionState Definitions, IMessageBus Bus)> CreateAsync(params SagaGraph[] graphs)
    {
        var bus = Substitute.For<IMessageBus>();
        var provider = Substitute.For<ISagaDefinitionProvider>();
        var loaded = new LoadedAssembly { Assembly = typeof(SagaInstanceStateTests).Assembly, Context = AssemblyLoadContext.Default };
        provider.Discover(loaded).Returns(graphs.Select(g => new SagaDefinitionResult(g, g.TypeName, null)).ToList());
        var definitions = new SagaDefinitionState([provider], bus);
        await definitions.AddAssemblyAsync(loaded);
        bus.ClearReceivedCalls();
        return (new SagaInstanceState([new StubSpanMapper()], definitions, bus), definitions, bus);
    }

    [Fact(DisplayName = "Resolves the graph by the span's type hint")]
    public async Task Resolves_By_Hint()
    {
        var (state, _, _) = await CreateAsync(Graph(OrderType, "Initial", "Submitted"), Graph(PaymentType, "Initial", "Submitted"));
        var sagaId = Guid.NewGuid();

        await state.IngestAsync([StubSpanMapper.Span(sagaId, "Initial", "Submitted", hint: "PaymentStateMachine")]);

        state.GetInstances(PaymentType).Should().ContainSingle(i => i.SagaId == sagaId && i.CurrentState == "Submitted");
        state.GetInstances(OrderType).Should().BeEmpty();
    }

    [Fact(DisplayName = "Without a hint, resolves the single graph containing both states")]
    public async Task Resolves_By_State_Set()
    {
        var (state, _, _) = await CreateAsync(Graph(OrderType, "Initial", "Submitted"), Graph(PaymentType, "Initial", "Authorized"));
        var sagaId = Guid.NewGuid();

        await state.IngestAsync([StubSpanMapper.Span(sagaId, "Initial", "Authorized")]);

        state.GetInstances(PaymentType).Should().ContainSingle(i => i.SagaId == sagaId);
        state.UnmatchedCount.Should().Be(0);
    }

    [Fact(DisplayName = "Ambiguous or unknown transitions land in the unmatched bucket and are counted")]
    public async Task Ambiguous_Goes_To_Unmatched()
    {
        var (state, _, _) = await CreateAsync(Graph(OrderType, "Initial", "Submitted"), Graph(PaymentType, "Initial", "Submitted"));

        await state.IngestAsync([
            StubSpanMapper.Span(Guid.NewGuid(), "Initial", "Submitted"),
            StubSpanMapper.Span(Guid.NewGuid(), "Initial", "Nowhere"),
            new ReceivedSpan("t", "s", null, "plain", "svc", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new Dictionary<string, string>()),
        ]);

        state.SpansReceived.Should().Be(3);
        state.SpansMapped.Should().Be(2);
        state.UnmatchedCount.Should().Be(2);
        state.Unmatched.Should().HaveCount(2);
        state.GetInstances(OrderType).Should().BeEmpty();
    }

    [Fact(DisplayName = "Transitions accumulate on one instance in order and update the current state")]
    public async Task Accumulates_Transitions()
    {
        var (state, _, _) = await CreateAsync(Graph(OrderType, "Initial", "Submitted", "Accepted"));
        var sagaId = Guid.NewGuid();
        var t0 = DateTimeOffset.UtcNow;

        await state.IngestAsync([StubSpanMapper.Span(sagaId, "Initial", "Submitted", at: t0)]);
        await state.IngestAsync([StubSpanMapper.Span(sagaId, "Submitted", "Accepted", at: t0.AddSeconds(1))]);

        var instance = state.GetInstance(OrderType, sagaId)!;
        instance.CurrentState.Should().Be("Accepted");
        instance.Transitions.Select(t => t.EndState).Should().Equal("Submitted", "Accepted");
        instance.FirstSeen.Should().Be(t0);
        instance.LastSeen.Should().Be(t0.AddSeconds(1));
    }

    [Fact(DisplayName = "Instances are listed newest first")]
    public async Task Lists_Newest_First()
    {
        var (state, _, _) = await CreateAsync(Graph(OrderType, "Initial", "Submitted"));
        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();
        var t0 = DateTimeOffset.UtcNow;

        await state.IngestAsync([
            StubSpanMapper.Span(older, "Initial", "Submitted", at: t0),
            StubSpanMapper.Span(newer, "Initial", "Submitted", at: t0.AddSeconds(5)),
        ]);

        state.GetInstances(OrderType).Select(i => i.SagaId).Should().Equal(newer, older);
    }

    [Fact(DisplayName = "Transitions per instance are capped, dropping the oldest")]
    public async Task Caps_Transitions()
    {
        var (state, _, _) = await CreateAsync(Graph(OrderType, "Initial", "A", "B"));
        var sagaId = Guid.NewGuid();
        var spans = Enumerable.Range(0, SagaInstanceState.MaxTransitionsPerInstance + 5)
            .Select(i => StubSpanMapper.Span(sagaId, i % 2 == 0 ? "A" : "B", i % 2 == 0 ? "B" : "A", spanId: $"span{i:D12}"))
            .ToList();

        await state.IngestAsync(spans);

        var instance = state.GetInstance(OrderType, sagaId)!;
        instance.Transitions.Should().HaveCount(SagaInstanceState.MaxTransitionsPerInstance);
        instance.Transitions[0].SpanId.Should().Be(spans[5].SpanId);
    }

    [Fact(DisplayName = "Instances per graph are capped, evicting the least recently seen")]
    public async Task Caps_Instances()
    {
        var (state, _, _) = await CreateAsync(Graph(OrderType, "Initial", "Submitted"));
        var t0 = DateTimeOffset.UtcNow;
        var ids = Enumerable.Range(0, SagaInstanceState.MaxInstancesPerGraph + 1).Select(_ => Guid.NewGuid()).ToList();

        await state.IngestAsync(ids.Select((id, i) => StubSpanMapper.Span(id, "Initial", "Submitted", at: t0.AddMilliseconds(i))).ToList());

        var instances = state.GetInstances(OrderType);
        instances.Should().HaveCount(SagaInstanceState.MaxInstancesPerGraph);
        instances.Select(i => i.SagaId).Should().NotContain(ids[0]);
    }

    [Fact(DisplayName = "Publishes SagaInstancesChanged once per ingested batch")]
    public async Task Publishes_Once_Per_Batch()
    {
        var (state, _, bus) = await CreateAsync(Graph(OrderType, "Initial", "Submitted"));

        await state.IngestAsync([
            StubSpanMapper.Span(Guid.NewGuid(), "Initial", "Submitted"),
            StubSpanMapper.Span(Guid.NewGuid(), "Initial", "Submitted"),
        ]);

        await bus.Received(1).PublishAsync(Arg.Any<SagaInstancesChanged>());
    }

    [Fact(DisplayName = "Removing graphs drops their instances")]
    public async Task Remove_Graphs_Drops_Instances()
    {
        var (state, _, _) = await CreateAsync(Graph(OrderType, "Initial", "Submitted"));
        await state.IngestAsync([StubSpanMapper.Span(Guid.NewGuid(), "Initial", "Submitted")]);

        await state.RemoveGraphsAsync([OrderType]);

        state.GetInstances(OrderType).Should().BeEmpty();
    }
}
