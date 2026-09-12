using Iris.Telemetry;
using MassTransit.Logging;

namespace Iris.Sagas.MassTransit;

/// <summary>
/// MassTransit's StateMachineSagaMessageFilter stamps the saga id, begin state and end state on the
/// consume span, and StartSagaStateMachineActivity stamps the state machine's simple class name as the
/// consumer type. That is everything a transition needs.
/// </summary>
public sealed class MassTransitSpanMapper : ISagaSpanMapper
{
    private const string UrnPrefix = "urn:message:";

    public string Framework => "MassTransit";

    public bool TryMap(ReceivedSpan span, out SagaTransition transition)
    {
        transition = default!;

        if (!span.Tags.TryGetValue(DiagnosticHeaders.SagaId, out var sagaIdText)
            || !span.Tags.TryGetValue(DiagnosticHeaders.BeginState, out var beginState)
            || !span.Tags.TryGetValue(DiagnosticHeaders.EndState, out var endState)
            || !Guid.TryParse(sagaIdText, out var sagaId))
            return false;

        span.Tags.TryGetValue(DiagnosticHeaders.ConsumerType, out var typeHint);

        transition = new SagaTransition(
            SagaTypeHint: string.IsNullOrWhiteSpace(typeHint) ? null : typeHint,
            SagaId: sagaId,
            BeginState: beginState,
            EndState: endState,
            EventName: ResolveEventName(span),
            TraceId: span.TraceId,
            SpanId: span.SpanId,
            Timestamp: span.EndTime);
        return true;
    }

    private static string ResolveEventName(ReceivedSpan span)
    {
        if (!span.Tags.TryGetValue(DiagnosticHeaders.MessageTypes, out var messageTypes) || string.IsNullOrWhiteSpace(messageTypes))
            return span.Name;

        var first = messageTypes.Split(',', 2)[0].Trim();
        if (first.StartsWith(UrnPrefix, StringComparison.Ordinal))
            first = first[UrnPrefix.Length..];

        var lastSeparator = first.LastIndexOf(':');
        return lastSeparator >= 0 ? first[(lastSeparator + 1)..] : first;
    }
}
