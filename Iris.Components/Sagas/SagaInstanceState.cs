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

    public async Task IngestAsync(IReadOnlyList<ReceivedSpan> spans)
    {
        if (spans.Count == 0)
            return;

        // Read before taking our own lock: SagaDefinitionState locks its own gate to serve this.
        var graphs = _definitions.Graphs;
        lock (_gate)
        {
            foreach (var span in spans)
            {
                SpansReceived++;
                if (!TryMap(span, out var transition))
                    continue;
                SpansMapped++;

                var graph = Resolve(transition, graphs);
                if (graph is null)
                {
                    UnmatchedCount++;
                    _unmatched.Add(transition);
                    if (_unmatched.Count > MaxUnmatched)
                        _unmatched.RemoveAt(0);
                    continue;
                }

                Append(graph.TypeName, transition);
            }
        }

        await _bus.PublishAsync(new SagaInstancesChanged());
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
