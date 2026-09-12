using MassTransit;

namespace Iris.Samples.MassTransitSaga;

public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
}
