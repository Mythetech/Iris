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

    [Fact(DisplayName = "Only the most recent transitions keep their span, so instances cannot hoard them")]
    public async Task Drops_Spans_Beyond_The_Retained_Window()
    {
        var (state, _, _) = await CreateAsync(Graph(OrderType, "Initial", "Submitted"));
        var sagaId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow;
        var total = SagaInstanceState.MaxRetainedSpansPerInstance + 3;

        for (var i = 0; i < total; i++)
            await state.IngestAsync([StubSpanMapper.Span(sagaId, "Initial", "Submitted", hint: "OrderStateMachine", at: start.AddSeconds(i))]);

        var transitions = state.GetInstance(OrderType, sagaId)!.Transitions;
        transitions.Should().HaveCount(total);
        transitions.TakeLast(SagaInstanceState.MaxRetainedSpansPerInstance).Should().OnlyContain(t => t.Span != null,
            "the recent end of the timeline is what anyone actually inspects");
        transitions.Take(3).Should().OnlyContain(t => t.Span == null,
            "a span is far heavier than the transition read out of it, so old ones must not pile up");
        transitions.Should().OnlyContain(t => t.TraceId == "trace",
            "identity is not evidence; the ids stay whatever happens to the span");
    }

    [Fact(DisplayName = "Reports what became of every span in the batch, in the order they arrived")]
    public async Task Reports_An_Outcome_Per_Span()
    {
        var (state, _, _) = await CreateAsync(Graph(OrderType, "Initial", "Submitted"), Graph(PaymentType, "Initial", "Submitted"));
        var plain = new ReceivedSpan("trace", "span", null, "GET /orders", "svc", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new Dictionary<string, string>());
        var mapped = StubSpanMapper.Span(Guid.NewGuid(), "Initial", "Submitted", hint: "OrderStateMachine");
        var ambiguous = StubSpanMapper.Span(Guid.NewGuid(), "Initial", "Submitted");

        var outcomes = await state.IngestAsync([plain, mapped, ambiguous]);

        outcomes.Should().HaveCount(3, "a span the receiver decoded is worth showing even when Iris made nothing of it");
        outcomes[0].Should().Be(new SpanIngestOutcome(plain, SpanIngest.NotASagaSpan));
        outcomes[1].Should().Be(new SpanIngestOutcome(mapped, SpanIngest.Mapped));
        outcomes[2].Should().Be(new SpanIngestOutcome(ambiguous, SpanIngest.Unmatched));
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

    [Fact(DisplayName = "A span arriving late does not rewind the current state, the timestamps, or the timeline order")]
    public async Task Late_Span_Does_Not_Rewind_Instance()
    {
        var (state, _, _) = await CreateAsync(Graph(OrderType, "Initial", "Submitted", "Accepted"));
        var sagaId = Guid.NewGuid();
        var t0 = DateTimeOffset.UtcNow;

        await state.IngestAsync([StubSpanMapper.Span(sagaId, "Submitted", "Accepted", at: t0.AddSeconds(1))]);
        await state.IngestAsync([StubSpanMapper.Span(sagaId, "Initial", "Submitted", at: t0)]);

        var instance = state.GetInstance(OrderType, sagaId)!;
        instance.CurrentState.Should().Be("Accepted");
        instance.FirstSeen.Should().Be(t0);
        instance.LastSeen.Should().Be(t0.AddSeconds(1));
        instance.Transitions.Select(t => t.EndState).Should().Equal("Submitted", "Accepted");
    }

    [Fact(DisplayName = "A hint shared by two graphs is ambiguous and lands in the unmatched bucket")]
    public async Task Ambiguous_Hint_Goes_To_Unmatched()
    {
        var (state, _, _) = await CreateAsync(
            Graph("Ordering.OrderStateMachine", "Initial", "Submitted"),
            Graph("Shipping.OrderStateMachine", "Initial", "Submitted"));

        await state.IngestAsync([StubSpanMapper.Span(Guid.NewGuid(), "Initial", "Submitted", hint: "OrderStateMachine")]);

        state.GetInstances("Ordering.OrderStateMachine").Should().BeEmpty();
        state.GetInstances("Shipping.OrderStateMachine").Should().BeEmpty();
        state.UnmatchedCount.Should().Be(1);
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
