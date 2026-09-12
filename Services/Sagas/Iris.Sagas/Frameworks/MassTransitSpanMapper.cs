using System.Diagnostics.CodeAnalysis;
using Iris.Telemetry;
using MassTransit.Logging;

namespace Iris.Sagas.Frameworks;

/// <summary>
/// MassTransit's StateMachineSagaMessageFilter stamps the saga id, begin state and end state on the
/// consume span, and StartSagaStateMachineActivity stamps the state machine's simple class name as the
/// consumer type. That is everything a transition needs.
/// </summary>
public sealed class MassTransitSpanMapper : ISagaSpanMapper
{
    private const string UrnPrefix = "urn:message:";

    public string Framework => "MassTransit";

    public bool TryMap(ReceivedSpan span, [MaybeNullWhen(false)] out SagaTransition transition)
    {
        transition = default;

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
            Timestamp: span.EndTime,
            Span: span);
        return true;
    }

    private static string ResolveEventName(ReceivedSpan span)
    {
        if (!span.Tags.TryGetValue(DiagnosticHeaders.MessageTypes, out var messageTypes) || string.IsNullOrWhiteSpace(messageTypes))
            return span.Name;

        var first = FirstUrn(messageTypes).Trim();
        if (first.StartsWith(UrnPrefix, StringComparison.Ordinal))
            first = first[UrnPrefix.Length..];

        var lastSeparator = LastTopLevelColon(first);
        return lastSeparator >= 0 ? first[(lastSeparator + 1)..] : first;
    }

    // MassTransit renders generic type arguments as bracketed groups, e.g.
    // urn:message:Ns:Wrapper[[A:X],[B:Y]], and those groups contain commas of their own. Only a
    // comma outside every bracket actually separates two distinct URNs in a joined message_types tag.
    private static string FirstUrn(string messageTypes)
    {
        var depth = 0;
        for (var i = 0; i < messageTypes.Length; i++)
        {
            switch (messageTypes[i])
            {
                case '[':
                    depth++;
                    break;
                case ']':
                    depth--;
                    break;
                case ',' when depth == 0:
                    return messageTypes[..i];
            }
        }

        return messageTypes;
    }

    // The same bracketed generic arguments contain their own namespace:type colons, so the
    // namespace/type separator for the URN itself is the last colon found outside any bracket.
    private static int LastTopLevelColon(string urn)
    {
        var depth = 0;
        var lastIndex = -1;
        for (var i = 0; i < urn.Length; i++)
        {
            switch (urn[i])
            {
                case '[':
                    depth++;
                    break;
                case ']':
                    depth--;
                    break;
                case ':' when depth == 0:
                    lastIndex = i;
                    break;
            }
        }

        return lastIndex;
    }
}
