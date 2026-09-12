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
/// CancelTestOrder is valid in two states so the provider's edge pairing is exercised on a shared event.
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
    }

    public State Submitted { get; private set; } = null!;
    public State Accepted { get; private set; } = null!;
    public State Shipped { get; private set; } = null!;
    public State Cancelled { get; private set; } = null!;

    public Event<SubmitTestOrder> Submit { get; private set; } = null!;
    // MassTransit derives the event name from the property name, and this fixture needs an event
    // literally named Accept, which collides with MassTransitStateMachine.Accept(StateMachineVisitor).
    // Hiding it is harmless because the graph visitor reaches Accept through the StateMachine interface.
    public new Event<AcceptTestOrder> Accept { get; private set; } = null!;
    public Event<ShipTestOrder> Ship { get; private set; } = null!;
    public Event<CancelTestOrder> Cancel { get; private set; } = null!;
}
