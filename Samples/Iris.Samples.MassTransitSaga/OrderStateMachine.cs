using Iris.Samples.MassTransitSaga.Contracts;
using MassTransit;

namespace Iris.Samples.MassTransitSaga;

/// <summary>
/// Submitted, Accepted, Shipped, Cancelled. CancelOrder is valid from Submitted and Accepted so the
/// graph has a shared event and a branch.
/// </summary>
public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderSubmitted, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => OrderAccepted, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => OrderShipped, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => OrderCancelled, x => x.CorrelateById(m => m.Message.OrderId));

        Initially(
            When(OrderSubmitted)
                .Then(ctx => Console.WriteLine($"[saga {ctx.Saga.CorrelationId}] submitted"))
                .TransitionTo(Submitted));

        During(Submitted,
            When(OrderAccepted)
                .Then(ctx => Console.WriteLine($"[saga {ctx.Saga.CorrelationId}] accepted"))
                .TransitionTo(Accepted),
            When(OrderCancelled)
                .Then(ctx => Console.WriteLine($"[saga {ctx.Saga.CorrelationId}] cancelled"))
                .TransitionTo(Cancelled));

        During(Accepted,
            When(OrderShipped)
                .Then(ctx => Console.WriteLine($"[saga {ctx.Saga.CorrelationId}] shipped"))
                .TransitionTo(Shipped),
            When(OrderCancelled)
                .Then(ctx => Console.WriteLine($"[saga {ctx.Saga.CorrelationId}] cancelled"))
                .TransitionTo(Cancelled));
    }

    public State Submitted { get; private set; } = null!;
    public State Accepted { get; private set; } = null!;
    public State Shipped { get; private set; } = null!;
    public State Cancelled { get; private set; } = null!;

    public Event<SubmitOrder> OrderSubmitted { get; private set; } = null!;
    public Event<AcceptOrder> OrderAccepted { get; private set; } = null!;
    public Event<ShipOrder> OrderShipped { get; private set; } = null!;
    public Event<CancelOrder> OrderCancelled { get; private set; } = null!;
}
