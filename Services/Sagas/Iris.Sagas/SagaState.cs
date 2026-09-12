namespace Iris.Sagas;

public sealed record SagaState(string Name, bool IsInitial, bool IsFinal);
