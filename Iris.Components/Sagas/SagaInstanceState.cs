using System.Diagnostics.CodeAnalysis;
using Iris.Components.Sagas.Messages;
using Iris.Sagas;
using Iris.Telemetry;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Iris.Components.Sagas;

/// <summary>
/// Live saga instances assembled from received spans. Bounded and in-memory; cleared on restart.
/// Fed by <see cref="Consumers.SpanBatchConsumer"/>; announces changes with <see cref="SagaInstancesChanged"/>.
/// </summary>
public sealed class SagaInstanceState
{
    public const int MaxInstancesPerGraph = 1000;
    public const int MaxTransitionsPerInstance = 200;
    public const int MaxUnmatched = 200;

    /// <summary>
    /// How many of an instance's transitions keep the span they were read from. A span carries the
    /// whole tag dictionary and is an order of magnitude heavier than the transition itself, so
    /// letting every retained transition hold one would let a busy exporter grow Iris without bound.
    /// Only the recent end of a timeline is ever inspected, so that is all that keeps its evidence.
    /// </summary>
    public const int MaxRetainedSpansPerInstance = 20;

    private readonly IReadOnlyList<ISagaSpanMapper> _mappers;
    private readonly SagaDefinitionState _definitions;
    private readonly IMessageBus _bus;
    private readonly object _gate = new();
    private readonly Dictionary<string, Dictionary<Guid, SagaInstance>> _instances = new(StringComparer.Ordinal);
    private readonly List<SagaTransition> _unmatched = [];

    public SagaInstanceState(IEnumerable<ISagaSpanMapper> mappers, SagaDefinitionState definitions, IMessageBus bus)
    {
        _mappers = mappers.ToList();
        _definitions = definitions;
        _bus = bus;
    }

    public int SpansReceived { get; private set; }
    public int SpansMapped { get; private set; }
    public int UnmatchedCount { get; private set; }

    public IReadOnlyList<SagaTransition> Unmatched
    {
        get { lock (_gate) return _unmatched.ToList(); }
    }

    public IReadOnlyList<SagaInstance> GetInstances(string graphTypeName)
    {
        lock (_gate)
        {
            return _instances.TryGetValue(graphTypeName, out var byId)
                ? byId.Values.OrderByDescending(i => i.LastSeen).ToList()
                : [];
        }
    }

    public SagaInstance? GetInstance(string graphTypeName, Guid sagaId)
    {
        lock (_gate)
            return _instances.TryGetValue(graphTypeName, out var byId) && byId.TryGetValue(sagaId, out var instance) ? instance : null;
    }

    /// <summary>
    /// Folds a batch of spans into the live instances, returning what became of each one in arrival
    /// order. A span Iris makes nothing of is still worth showing, so the caller can log the whole
    /// batch rather than only the part that mapped.
    /// </summary>
    public async Task<IReadOnlyList<SpanIngestOutcome>> IngestAsync(IReadOnlyList<ReceivedSpan> spans)
    {
        if (spans.Count == 0)
            return [];

        var outcomes = new List<SpanIngestOutcome>(spans.Count);

        // Read before taking our own lock: SagaDefinitionState locks its own gate to serve this.
        var graphs = _definitions.Graphs;
        lock (_gate)
        {
            foreach (var span in spans)
            {
                SpansReceived++;
                if (!TryMap(span, out var transition))
                {
                    outcomes.Add(new SpanIngestOutcome(span, SpanIngest.NotASagaSpan));
                    continue;
                }
                SpansMapped++;

                var graph = Resolve(transition, graphs);
                if (graph is null)
                {
                    UnmatchedCount++;
                    _unmatched.Add(transition);
                    if (_unmatched.Count > MaxUnmatched)
                        _unmatched.RemoveAt(0);
                    outcomes.Add(new SpanIngestOutcome(span, SpanIngest.Unmatched));
                    continue;
                }

                Append(graph.TypeName, transition);
                outcomes.Add(new SpanIngestOutcome(span, SpanIngest.Mapped));
            }
        }

        await _bus.PublishAsync(new SagaInstancesChanged());
        return outcomes;
    }

    public async Task RemoveGraphsAsync(IEnumerable<string> typeNames)
    {
        lock (_gate)
        {
            foreach (var typeName in typeNames)
                _instances.Remove(typeName);
        }
        await _bus.PublishAsync(new SagaInstancesChanged());
    }

    public async Task ClearAsync()
    {
        lock (_gate)
        {
            _instances.Clear();
            _unmatched.Clear();
            SpansReceived = 0;
            SpansMapped = 0;
            UnmatchedCount = 0;
        }
        await _bus.PublishAsync(new SagaInstancesChanged());
    }

    private bool TryMap(ReceivedSpan span, [MaybeNullWhen(false)] out SagaTransition transition)
    {
        foreach (var mapper in _mappers)
        {
            if (mapper.TryMap(span, out transition))
                return true;
        }
        transition = default;
        return false;
    }

    private static SagaGraph? Resolve(SagaTransition transition, IReadOnlyList<SagaGraph> graphs)
    {
        if (transition.SagaTypeHint is { } hint)
        {
            var byTypeName = graphs.FirstOrDefault(g => g.TypeName == hint);
            if (byTypeName is not null)
                return byTypeName;

            // The hint carries only the simple class name, and Iris loads arbitrary user assemblies,
            // so two state machines can share one display name. Refuse to guess between them and
            // fall through, the same way the state set rule below refuses to guess.
            var byDisplayName = graphs.Where(g => g.DisplayName == hint).ToList();
            if (byDisplayName.Count == 1)
                return byDisplayName[0];
        }

        var byStates = graphs
            .Where(g => g.States.Any(s => s.Name == transition.BeginState) && g.States.Any(s => s.Name == transition.EndState))
            .ToList();
        return byStates.Count == 1 ? byStates[0] : null;
    }

    /// <summary>
    /// Position that keeps the list ordered by timestamp, placing ties after the transitions already
    /// stored so arrival order still breaks them. Scans from the end because spans usually arrive in order.
    /// </summary>
    private static int OrderedInsertIndex(List<SagaTransition> transitions, DateTimeOffset timestamp)
    {
        for (var i = transitions.Count - 1; i >= 0; i--)
        {
            if (transitions[i].Timestamp <= timestamp)
                return i + 1;
        }
        return 0;
    }

    /// <summary>
    /// Clears the span off every transition but the most recent <see cref="MaxRetainedSpansPerInstance"/>.
    /// Only walks far enough back to find one already cleared: everything below that was cleared on an
    /// earlier append, so a long timeline does not get rewritten on every span that arrives.
    /// </summary>
    private static void DropAgedSpans(List<SagaTransition> transitions)
    {
        for (var i = transitions.Count - MaxRetainedSpansPerInstance - 1; i >= 0; i--)
        {
            if (transitions[i].Span is null)
                return;
            transitions[i] = transitions[i] with { Span = null };
        }
    }

    private void Append(string graphTypeName, SagaTransition transition)
    {
        if (!_instances.TryGetValue(graphTypeName, out var byId))
            _instances[graphTypeName] = byId = new Dictionary<Guid, SagaInstance>();

        if (byId.TryGetValue(transition.SagaId, out var existing))
        {
            var transitions = existing.Transitions.ToList();
            transitions.Insert(OrderedInsertIndex(transitions, transition.Timestamp), transition);
            if (transitions.Count > MaxTransitionsPerInstance)
                transitions.RemoveRange(0, transitions.Count - MaxTransitionsPerInstance);
            DropAgedSpans(transitions);

            // Spans have no delivery order, so a late arrival must not rewind the instance.
            var isLatest = transition.Timestamp >= existing.LastSeen;
            byId[transition.SagaId] = existing with
            {
                CurrentState = isLatest ? transition.EndState : existing.CurrentState,
                FirstSeen = transition.Timestamp < existing.FirstSeen ? transition.Timestamp : existing.FirstSeen,
                LastSeen = isLatest ? transition.Timestamp : existing.LastSeen,
                Transitions = transitions,
            };
            return;
        }

        if (byId.Count >= MaxInstancesPerGraph)
        {
            var oldest = byId.Values.MinBy(i => i.LastSeen)!;
            byId.Remove(oldest.SagaId);
        }

        byId[transition.SagaId] = new SagaInstance(
            transition.SagaId, graphTypeName, transition.EndState, transition.Timestamp, transition.Timestamp, [transition]);
    }
}
