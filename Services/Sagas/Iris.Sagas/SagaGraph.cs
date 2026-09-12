namespace Iris.Sagas;

public sealed record SagaGraph(
    string TypeName,
    string DisplayName,
    string Framework,
    IReadOnlyList<SagaState> States,
    IReadOnlyList<SagaTransitionDefinition> Transitions);
