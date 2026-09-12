namespace Iris.Sagas;

public sealed record SagaTransitionDefinition(string FromState, string ToState, string EventName);
