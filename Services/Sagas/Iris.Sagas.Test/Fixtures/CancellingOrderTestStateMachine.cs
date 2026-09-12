using MassTransit;

namespace Iris.Sagas.Test.Fixtures;

public record SubmitCancellingOrder(Guid OrderId);
public record AcceptCancellingOrder(Guid OrderId);
public record ShipCancellingOrder(Guid OrderId);
public record CancelCancellingOrder(Guid OrderId);

public class CancellingOrderTestState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
}

/// <summary>
/// The shape the MassTransit sample ships: one event handled from two states that both transition
/// and finalize, so four transitions carry the same event name and two pairs leave the same state
/// for different targets. That is what crowds the labels the layout has to place.
/// </summary>
public class CancellingOrderTestStateMachine : MassTransitStateMachine<CancellingOrderTestState>
{
    public CancellingOrderTestStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderSubmitted, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => OrderAccepted, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => OrderShipped, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => OrderCancelled, x => x.CorrelateById(m => m.Message.OrderId));

        Initially(
            When(OrderSubmitted).TransitionTo(Submitted));

        During(Submitted,
            When(OrderAccepted).TransitionTo(Accepted),
            When(OrderCancelled).TransitionTo(Cancelled).Finalize());

        During(Accepted,
            When(OrderShipped).TransitionTo(Shipped),
            When(OrderCancelled).TransitionTo(Cancelled).Finalize());

        SetCompletedWhenFinalized();
    }

    public State Submitted { get; private set; } = null!;
    public State Accepted { get; private set; } = null!;
    public State Shipped { get; private set; } = null!;
    public State Cancelled { get; private set; } = null!;

    public Event<SubmitCancellingOrder> OrderSubmitted { get; private set; } = null!;
    public Event<AcceptCancellingOrder> OrderAccepted { get; private set; } = null!;
    public Event<ShipCancellingOrder> OrderShipped { get; private set; } = null!;
    public Event<CancelCancellingOrder> OrderCancelled { get; private set; } = null!;
}
