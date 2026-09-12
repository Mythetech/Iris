using Iris.Telemetry;

namespace Iris.Sagas;

/// <param name="Span">
/// The span this transition was read out of, kept so the UI can show the evidence behind it.
/// Null once dropped: spans are far heavier than the transition itself, so only the most recent
/// few per instance keep theirs. The ids above are the transition's identity and never evict.
/// </param>
public sealed record SagaTransition(
    string? SagaTypeHint,
    Guid SagaId,
    string BeginState,
    string EndState,
    string? EventName,
    string TraceId,
    string SpanId,
    DateTimeOffset Timestamp,
    ReceivedSpan? Span = null);
