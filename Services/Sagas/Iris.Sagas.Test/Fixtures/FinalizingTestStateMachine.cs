using MassTransit;

namespace Iris.Sagas.Test.Fixtures;

public record StartFinalizing(Guid CorrelationId) : CorrelatedBy<Guid>;

public class FinalizingTestState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
}

public class FinalizingTestStateMachine : MassTransitStateMachine<FinalizingTestState>
{
    public FinalizingTestStateMachine()
    {
        InstanceState(x => x.CurrentState);
        Initially(When(Start).Finalize());
        SetCompletedWhenFinalized();
    }

    public Event<StartFinalizing> Start { get; private set; } = null!;
}
