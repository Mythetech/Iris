using Bunit;
using FluentAssertions;
using Iris.Components.Sagas;
using Iris.Telemetry;

namespace Iris.Components.Test.Sagas;

public class SpanDetailTests : IrisTestContext
{
    public SpanDetailTests()
    {
        Render<MudBlazor.MudPopoverProvider>();
    }

    private static ReceivedSpan Span(IDictionary<string, string>? tags = null)
    {
        var start = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);
        return new ReceivedSpan(
            TraceId: "0102030405060708090a0b0c0d0e0f10",
            SpanId: "1020304050607080",
            ParentSpanId: null,
            Name: "consume AcceptOrder",
            ServiceName: "Iris.Samples.MassTransitSaga",
            StartTime: start,
            EndTime: start.AddMilliseconds(4),
            Tags: tags is null
                ? new Dictionary<string, string> { ["zeta"] = "last", ["alpha"] = "first" }
                : new Dictionary<string, string>(tags));
    }

    [Fact(DisplayName = "Shows the span's name, service and how long it took")]
    public void Shows_The_Header()
    {
        var cut = Render<SpanDetail>(p => p.Add(x => x.Span, Span()));

        cut.Markup.Should().Contain("consume AcceptOrder").And.Contain("Iris.Samples.MassTransitSaga");
        cut.Find(".span-duration").TextContent.Should().Contain("4");
    }

    [Fact(DisplayName = "Shows the full trace and span ids, not the timeline's truncation")]
    public void Shows_Full_Ids()
    {
        var cut = Render<SpanDetail>(p => p.Add(x => x.Span, Span()));

        cut.Markup.Should().Contain("0102030405060708090a0b0c0d0e0f10").And.Contain("1020304050607080");
    }

    [Fact(DisplayName = "Lists every tag, ordered by name so the same span always reads the same way")]
    public void Lists_Tags_In_A_Stable_Order()
    {
        var cut = Render<SpanDetail>(p => p.Add(x => x.Span, Span()));

        cut.FindAll(".span-tag-key").Select(e => e.TextContent.Trim()).Should().Equal("alpha", "zeta");
        cut.FindAll(".span-tag-value").Select(e => e.TextContent.Trim()).Should().Equal("first", "last");
    }

    [Fact(DisplayName = "Says so rather than showing an empty table when a span carries no tags")]
    public void Explains_An_Untagged_Span()
    {
        var cut = Render<SpanDetail>(p => p.Add(x => x.Span, Span(new Dictionary<string, string>())));

        cut.FindAll(".span-tag-key").Should().BeEmpty();
        cut.Markup.Should().Contain("No tags");
    }
}
