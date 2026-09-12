using Iris.Sagas;

namespace Iris.Components.Sagas;

public sealed record SagaInstance(
    Guid SagaId,
    string GraphTypeName,
    string CurrentState,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen,
    IReadOnlyList<SagaTransition> Transitions);
