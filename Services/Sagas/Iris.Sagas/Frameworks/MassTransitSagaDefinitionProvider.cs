using System.Reflection;
using Iris.Assemblies;
using MassTransit;
using MassTransit.SagaStateMachine;

namespace Iris.Sagas.Frameworks;

/// <summary>
/// Instantiates every <see cref="MassTransitStateMachine{TInstance}"/> in a loaded assembly and converts
/// MassTransit's own state machine graph into the framework neutral <see cref="SagaGraph"/>.
/// </summary>
public sealed class MassTransitSagaDefinitionProvider : ISagaDefinitionProvider
{
    public string Framework => "MassTransit";

    public IReadOnlyList<SagaDefinitionResult> Discover(LoadedAssembly assembly)
    {
        return LoadTypes(assembly.Assembly)
            .Where(IsStateMachine)
            .Select(Describe)
            .ToList();
    }

    private static IEnumerable<Type> LoadTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    private static bool IsStateMachine(Type type)
        => type is { IsClass: true, IsAbstract: false } && FindInstanceType(type) is not null;

    private static Type? FindInstanceType(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(MassTransitStateMachine<>))
                return current.GetGenericArguments()[0];
        }

        return null;
    }

    private SagaDefinitionResult Describe(Type type)
    {
        var typeName = type.FullName ?? type.Name;

        if (type.GetConstructor(Type.EmptyTypes) is null)
        {
            var wanted = type.GetConstructors().FirstOrDefault()?.GetParameters()
                .Select(p => $"{p.ParameterType.Name} {p.Name}") ?? [];
            return new SagaDefinitionResult(null, typeName,
                $"No parameterless constructor. Iris cannot supply: {string.Join(", ", wanted)}");
        }

        StateMachine machine;
        try
        {
            machine = (StateMachine)Activator.CreateInstance(type)!;
        }
        catch (Exception ex)
        {
            var reason = ex is TargetInvocationException { InnerException: { } inner } ? inner.Message : ex.Message;
            return new SagaDefinitionResult(null, typeName, $"Constructor threw: {reason}");
        }

        StateMachineGraph graph;
        try
        {
            graph = GetGraph(machine, FindInstanceType(type)!);
        }
        catch (Exception ex)
        {
            var reason = ex is TargetInvocationException { InnerException: { } inner } ? inner.Message : ex.Message;
            return new SagaDefinitionResult(null, typeName, $"Could not build the state machine graph: {reason}");
        }

        return new SagaDefinitionResult(ToSagaGraph(graph, typeName, machine), typeName, null);
    }

    private static StateMachineGraph GetGraph(StateMachine machine, Type instanceType)
    {
        var getGraph = typeof(GraphStateMachineExtensions)
            .GetMethod(nameof(GraphStateMachineExtensions.GetGraph))!
            .MakeGenericMethod(instanceType);

        return (StateMachineGraph)getGraph.Invoke(null, [machine])!;
    }

    private SagaGraph ToSagaGraph(StateMachineGraph graph, string typeName, StateMachine machine)
    {
        var initialName = machine.Initial.Name;
        var finalName = machine.Final.Name;

        var states = graph.Vertices
            .Where(v => v.VertexType == typeof(State))
            .Select(v => new SagaState(v.Title, v.Title == initialName, v.Title == finalName))
            .ToList();

        return new SagaGraph(typeName, machine.Name, Framework, states, MapTransitions(graph));
    }

    // MassTransit models an event as its own vertex, so one transition is the pair of edges
    // state -> event -> state, and the edge itself carries no event name. The visitor emits those two
    // edges back to back per behaviour, which is the only thing that says which source state a given
    // event target belongs to when the same event is handled in several states. The exception is that
    // the visitor collects edges in a HashSet, so a repeated event -> state edge (the same event
    // transitioning to the same state from two different source states) is dropped as a duplicate and
    // the trailing state -> event edge is left with no follower. Those fall back to every known target
    // of that event, which is unambiguous because the only reason the edge went missing is that some
    // earlier source state already reached the same target.
    private static List<SagaTransitionDefinition> MapTransitions(StateMachineGraph graph)
    {
        var edges = graph.Edges.ToList();
        var targetsByEvent = edges.Where(IsEventToState).ToLookup(e => e.From);

        var transitions = new List<SagaTransitionDefinition>();
        Vertex? sourceState = null;
        Vertex? currentEvent = null;

        for (var i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];

            if (IsStateToEvent(edge))
            {
                sourceState = edge.From;
                currentEvent = edge.To;

                var hasFollowingTarget = i + 1 < edges.Count
                    && IsEventToState(edges[i + 1])
                    && edges[i + 1].From.Equals(currentEvent);

                if (!hasFollowingTarget)
                {
                    transitions.AddRange(targetsByEvent[currentEvent]
                        .Select(target => new SagaTransitionDefinition(sourceState.Title, target.To.Title, currentEvent.Title)));
                }
            }
            else if (IsEventToState(edge) && sourceState is not null && edge.From.Equals(currentEvent))
            {
                transitions.Add(new SagaTransitionDefinition(sourceState.Title, edge.To.Title, edge.From.Title));
            }
        }

        return transitions.Distinct().ToList();
    }

    private static bool IsStateToEvent(Edge edge)
        => edge.From.VertexType == typeof(State) && edge.To.VertexType == typeof(Event);

    private static bool IsEventToState(Edge edge)
        => edge.From.VertexType == typeof(Event) && edge.To.VertexType == typeof(State);
}
