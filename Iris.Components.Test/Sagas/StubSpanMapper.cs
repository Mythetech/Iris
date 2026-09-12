using Iris.Sagas;
using Iris.Telemetry;

namespace Iris.Components.Test.Sagas;

/// <summary>Maps spans by plain tags so state tests do not depend on MassTransit tag names.</summary>
public sealed class StubSpanMapper : ISagaSpanMapper
{
    public string Framework => "Stub";

    public bool TryMap(ReceivedSpan span, out SagaTransition transition)
    {
        transition = default!;
        if (!span.Tags.TryGetValue("saga", out var saga) || !Guid.TryParse(saga, out var sagaId))
            return false;
        span.Tags.TryGetValue("hint", out var hint);
        transition = new SagaTransition(hint, sagaId, span.Tags["from"], span.Tags["to"], span.Tags.GetValueOrDefault("event"), span.TraceId, span.SpanId, span.EndTime);
        return true;
    }

    public static ReceivedSpan Span(Guid sagaId, string from, string to, string? hint = null, DateTimeOffset? at = null, string? spanId = null)
    {
        var tags = new Dictionary<string, string> { ["saga"] = sagaId.ToString(), ["from"] = from, ["to"] = to, ["event"] = $"{from}To{to}" };
        if (hint is not null) tags["hint"] = hint;
        var time = at ?? DateTimeOffset.UtcNow;
        return new ReceivedSpan("trace", spanId ?? Guid.NewGuid().ToString("N")[..16], null, "span", "svc", time, time, tags);
    }
}
