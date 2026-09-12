using Iris.Telemetry;

namespace Iris.Components.Sagas;

/// <summary>What <see cref="SagaInstanceState.IngestAsync"/> made of a single span.</summary>
public enum SpanIngest
{
    /// <summary>No mapper recognised it, so it described no saga transition at all.</summary>
    NotASagaSpan,

    /// <summary>It described a transition, but no single loaded state machine claimed it.</summary>
    Unmatched,

    /// <summary>It moved an instance of a loaded state machine.</summary>
    Mapped,
}

public sealed record SpanIngestOutcome(ReceivedSpan Span, SpanIngest Result);
