using System.Diagnostics.CodeAnalysis;
using MassTransit;
using MassTransit.SagaStateMachine;

namespace Iris.Sagas.Frameworks;

/// <summary>
/// Walks a MassTransit state machine and records one transition per transition activity.
/// </summary>
/// <remarks>
/// This deliberately does not go through MassTransit's own <c>GetGraph()</c> for transitions. That graph
/// models an event as its own vertex, so a transition is the edge pair state to event to state, and it
/// collects edges in a HashSet keyed on (from, to, title). When the same event moves two different source
/// states to the same target, the second event-to-state edge is value equal to the first and is dropped,
/// and the graph then no longer records which source state the surviving edge belonged to. Any attempt to
/// re-pair the survivors invents transitions the machine cannot actually perform. Visiting the machine
/// directly keeps each behaviour's source state, event and target state together, so nothing has to be
/// inferred.
/// </remarks>
internal sealed class MassTransitTransitionVisitor : StateMachineVisitor
{
    private static readonly string ToStatePropertyName = nameof(TransitionActivity<SagaStateMachineInstance>.ToState);

    private readonly List<SagaTransitionDefinition> _transitions = [];
    private State? _currentState;
    private Event? _currentEvent;

    public IReadOnlyList<SagaTransitionDefinition> Transitions => _transitions;

    public void Visit(State state, Action<State> next)
    {
        _currentState = state;
        next(state);
    }

    public void Visit(Event @event, Action<Event> next)
    {
        _currentEvent = @event;
        next(@event);
    }

    public void Visit<TMessage>(Event<TMessage> @event, Action<Event<TMessage>> next)
        where TMessage : class
    {
        _currentEvent = @event;
        next(@event);
    }

    // Leaf activities call the single argument overload and container activities call the two argument
    // one, so routing the former through the latter sees every activity exactly once.
    public void Visit(IStateMachineActivity activity) => Visit(activity, _ => { });

    public void Visit(IStateMachineActivity activity, Action<IStateMachineActivity> next)
    {
        if (_currentState is not null && _currentEvent is not null && TryReadToState(activity, out var toState))
            _transitions.Add(new SagaTransitionDefinition(_currentState.Name, toState.Name, _currentEvent.Name));

        next(activity);
    }

    public void Visit<T>(IBehavior<T> behavior)
        where T : class, SagaStateMachineInstance
        => Visit(behavior, _ => { });

    public void Visit<T>(IBehavior<T> behavior, Action<IBehavior<T>> next)
        where T : class, SagaStateMachineInstance
        => next(behavior);

    public void Visit<T, TMessage>(IBehavior<T, TMessage> behavior)
        where T : class, SagaStateMachineInstance
        where TMessage : class
        => Visit(behavior, _ => { });

    public void Visit<T, TMessage>(IBehavior<T, TMessage> behavior, Action<IBehavior<T, TMessage>> next)
        where T : class, SagaStateMachineInstance
        where TMessage : class
        => next(behavior);

    // TransitionActivity is closed over the saga instance type, which is only known at runtime here, and
    // it exposes ToState on no non generic interface, so the target is read by name off the closed type.
    private static bool TryReadToState(IStateMachineActivity activity, [MaybeNullWhen(false)] out State toState)
    {
        var activityType = activity.GetType();

        if (activityType.IsGenericType
            && activityType.GetGenericTypeDefinition() == typeof(TransitionActivity<>)
            && activityType.GetProperty(ToStatePropertyName)?.GetValue(activity) is State state)
        {
            toState = state;
            return true;
        }

        toState = null;
        return false;
    }
}
