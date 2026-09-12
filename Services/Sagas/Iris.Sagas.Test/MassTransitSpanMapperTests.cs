using FluentAssertions;
using Iris.Sagas;
using Iris.Sagas.Frameworks;
using Iris.Telemetry;

namespace Iris.Sagas.Test;

public class MassTransitSpanMapperTests
{
    private static readonly Guid SagaId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static ReceivedSpan Span(IDictionary<string, string> tags, string name = "order-state process")
        => new(
            TraceId: "0102030405060708090a0b0c0d0e0f10",
            SpanId: "1020304050607080",
            ParentSpanId: null,
            Name: name,
            ServiceName: "sample",
            StartTime: DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
            EndTime: DateTimeOffset.FromUnixTimeSeconds(1_700_000_001),
            Tags: new Dictionary<string, string>(tags));

    private static Dictionary<string, string> SagaTags() => new()
    {
        ["messaging.masstransit.saga_id"] = SagaId.ToString("D"),
        ["messaging.masstransit.begin_state"] = "Submitted",
        ["messaging.masstransit.end_state"] = "Accepted",
        ["messaging.masstransit.consumer_type"] = "OrderStateMachine",
        ["messaging.masstransit.message_types"] = "urn:message:Iris.Samples.MassTransitSaga.Contracts:AcceptOrder",
    };

    [Fact(DisplayName = "Maps a span carrying the three saga tags into a transition")]
    public void Maps_Saga_Span()
    {
        var mapper = new MassTransitSpanMapper();

        var mapped = mapper.TryMap(Span(SagaTags()), out var mappedTransition);
        var transition = mappedTransition!;

        mapped.Should().BeTrue();
        transition.SagaId.Should().Be(SagaId);
        transition.BeginState.Should().Be("Submitted");
        transition.EndState.Should().Be("Accepted");
        transition.SagaTypeHint.Should().Be("OrderStateMachine");
        transition.EventName.Should().Be("AcceptOrder");
        transition.TraceId.Should().Be("0102030405060708090a0b0c0d0e0f10");
        transition.SpanId.Should().Be("1020304050607080");
        transition.Timestamp.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1_700_000_001));
    }

    [Theory(DisplayName = "Ignores spans missing any of the three saga tags")]
    [InlineData("messaging.masstransit.saga_id")]
    [InlineData("messaging.masstransit.begin_state")]
    [InlineData("messaging.masstransit.end_state")]
    public void Ignores_Spans_Missing_Tags(string missing)
    {
        var tags = SagaTags();
        tags.Remove(missing);

        new MassTransitSpanMapper().TryMap(Span(tags), out _).Should().BeFalse();
    }

    [Fact(DisplayName = "Ignores spans whose saga id is not a Guid")]
    public void Ignores_Bad_Saga_Id()
    {
        var tags = SagaTags();
        tags["messaging.masstransit.saga_id"] = "not-a-guid";

        new MassTransitSpanMapper().TryMap(Span(tags), out _).Should().BeFalse();
    }

    [Fact(DisplayName = "Takes the first message type when several urns are comma joined")]
    public void Takes_First_Message_Type()
    {
        var tags = SagaTags();
        tags["messaging.masstransit.message_types"] = "urn:message:Contracts:AcceptOrder,urn:message:Contracts:IOrderCommand";

        new MassTransitSpanMapper().TryMap(Span(tags), out var mappedTransition);
        var transition = mappedTransition!;

        transition.EventName.Should().Be("AcceptOrder");
    }

    [Fact(DisplayName = "Falls back to the span name when there is no message type tag")]
    public void Falls_Back_To_Span_Name()
    {
        var tags = SagaTags();
        tags.Remove("messaging.masstransit.message_types");
        tags.Remove("messaging.masstransit.consumer_type");

        new MassTransitSpanMapper().TryMap(Span(tags), out var mappedTransition);
        var transition = mappedTransition!;

        transition.EventName.Should().Be("order-state process");
        transition.SagaTypeHint.Should().BeNull();
    }

    [Fact(DisplayName = "Resolves the type name from a two-argument generic message urn")]
    public void Resolves_Generic_Message_Type()
    {
        var tags = SagaTags();
        tags["messaging.masstransit.message_types"] = "urn:message:Ns:Wrapper[[A:X],[B:Y]]";

        new MassTransitSpanMapper().TryMap(Span(tags), out var mappedTransition);
        var transition = mappedTransition!;

        transition.EventName.Should().Be("Wrapper[[A:X],[B:Y]]");
    }

    [Fact(DisplayName = "Keeps a two-argument generic urn intact when comma-joined with a second urn")]
    public void Splits_Generic_Urn_At_Bracket_Depth_Zero()
    {
        var tags = SagaTags();
        tags["messaging.masstransit.message_types"] = "urn:message:Ns:Wrapper[[A:X],[B:Y]],urn:message:Contracts:IOrderCommand";

        new MassTransitSpanMapper().TryMap(Span(tags), out var mappedTransition);
        var transition = mappedTransition!;

        transition.EventName.Should().Be("Wrapper[[A:X],[B:Y]]");
    }
}
