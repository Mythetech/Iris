using MassTransit;

namespace Iris.Sagas.Test.Fixtures;

public interface IPricing;

public class DependentTestState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
}

/// <summary>Has no parameterless constructor, so the provider must report an error for it.</summary>
public class DependentTestStateMachine : MassTransitStateMachine<DependentTestState>
{
    public DependentTestStateMachine(IPricing pricing, int retries)
    {
        InstanceState(x => x.CurrentState);
    }
}
