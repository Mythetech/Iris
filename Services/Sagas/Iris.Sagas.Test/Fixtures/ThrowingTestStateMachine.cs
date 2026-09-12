using MassTransit;

namespace Iris.Sagas.Test.Fixtures;

public class ThrowingTestState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
}

/// <summary>Its constructor throws, so the provider must report the cause rather than let it escape.</summary>
public class ThrowingTestStateMachine : MassTransitStateMachine<ThrowingTestState>
{
    public ThrowingTestStateMachine()
    {
        throw new InvalidOperationException("no broker configured");
    }
}

public class StaticThrowingTestState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
}

/// <summary>
/// Its static constructor throws, so the runtime wraps the cause in a TypeInitializationException whose
/// own message says nothing useful. The provider must report the inner cause.
/// </summary>
public class StaticThrowingTestStateMachine : MassTransitStateMachine<StaticThrowingTestState>
{
    static StaticThrowingTestStateMachine()
    {
        throw new InvalidOperationException("static setup failed");
    }
}
