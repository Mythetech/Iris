namespace Iris.Components.Sagas.Messages;

public sealed record SagaDefinitionsChanged(IReadOnlyList<string> RemovedTypeNames);
