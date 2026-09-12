using MassTransit;

namespace Iris.Sagas.Test.Fixtures;

public record SubmitTestOrder(Guid OrderId);
public record AcceptTestOrder(Guid OrderId);
public record ShipTestOrder(Guid OrderId);
public record CancelTestOrder(Guid OrderId);

public class OrderTestState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
}

/// <summary>
/// CancelTestOrder is valid in three states, and it does not lead to the same target from all of them.
/// That is what discriminates a real per-behaviour transition walk from a naive cross product of an
/// event's source states against its target states: the cross product would invent Accepted to Failed
/// and Shipped to Cancelled, neither of which this machine can perform.
/// </summary>
public class OrderTestStateMachine : MassTransitStateMachine<OrderTestState>
{
    public OrderTestStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => Submit, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => Accept, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => Ship, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => Cancel, x => x.CorrelateById(m => m.Message.OrderId));

        Initially(
            When(Submit).TransitionTo(Submitted));

        During(Submitted,
            When(Accept).TransitionTo(Accepted),
            When(Cancel).TransitionTo(Cancelled));

        During(Accepted,
            When(Ship).TransitionTo(Shipped),
            When(Cancel).TransitionTo(Cancelled));

        During(Shipped,
            When(Cancel).TransitionTo(Failed));
    }

    public State Submitted { get; private set; } = null!;
    public State Accepted { get; private set; } = null!;
    public State Shipped { get; private set; } = null!;
    public State Cancelled { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Event<SubmitTestOrder> Submit { get; private set; } = null!;
    // MassTransit derives the event name from the property name, and this fixture needs an event
    // literally named Accept, which collides with MassTransitStateMachine.Accept(StateMachineVisitor).
    // Hiding it is harmless because Iris reaches Accept through the non generic StateMachine interface.
    public new Event<AcceptTestOrder> Accept { get; private set; } = null!;
    public Event<ShipTestOrder> Ship { get; private set; } = null!;
    public Event<CancelTestOrder> Cancel { get; private set; } = null!;
}
