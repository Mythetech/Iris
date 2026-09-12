namespace Iris.Sagas;

public sealed record SagaTransition(
    string? SagaTypeHint,
    Guid SagaId,
    string BeginState,
    string EndState,
    string? EventName,
    string TraceId,
    string SpanId,
    DateTimeOffset Timestamp);
