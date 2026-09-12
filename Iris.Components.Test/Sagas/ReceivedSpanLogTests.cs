using FluentAssertions;
using Iris.Components.Sagas;
using Iris.Components.Sagas.Messages;
using Iris.Telemetry;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;

namespace Iris.Components.Test.Sagas;

public class ReceivedSpanLogTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    private ReceivedSpanLog Create() => new(_bus);

    private static SpanIngestOutcome Outcome(string name, SpanIngest result = SpanIngest.NotASagaSpan)
    {
        var at = DateTimeOffset.UtcNow;
        return new SpanIngestOutcome(
            new ReceivedSpan("trace", name, null, name, "svc", at, at, new Dictionary<string, string>()),
            result);
    }

    [Fact(DisplayName = "Shows the most recent span first, whichever way ingest classified it")]
    public async Task Orders_Newest_First()
    {
        var log = Create();

        await log.RecordAsync([Outcome("first"), Outcome("second", SpanIngest.Mapped)]);
        await log.RecordAsync([Outcome("third", SpanIngest.Unmatched)]);

        log.Recent.Select(o => o.Span.Name).Should().Equal("third", "second", "first");
        log.Recent.Select(o => o.Result).Should().Equal(SpanIngest.Unmatched, SpanIngest.Mapped, SpanIngest.NotASagaSpan);
    }

    [Fact(DisplayName = "Keeps only the newest MaxSpans while the running total keeps climbing")]
    public async Task Bounds_The_Buffer()
    {
        var log = Create();
        var overflow = ReceivedSpanLog.MaxSpans + 5;

        await log.RecordAsync(Enumerable.Range(0, overflow).Select(i => Outcome($"span{i}")).ToList());

        log.Recent.Should().HaveCount(ReceivedSpanLog.MaxSpans);
        log.Received.Should().Be(overflow, "the total is what tells you the view is a window rather than the whole story");
        log.Recent[0].Span.Name.Should().Be($"span{overflow - 1}");
        log.Recent.Should().NotContain(o => o.Span.Name == "span0");
    }

    [Fact(DisplayName = "Clearing empties the buffer and resets the total")]
    public async Task Clears()
    {
        var log = Create();
        await log.RecordAsync([Outcome("first")]);

        await log.ClearAsync();

        log.Recent.Should().BeEmpty();
        log.Received.Should().Be(0);
    }

    [Fact(DisplayName = "Announces its own change rather than relying on the instance state's")]
    public async Task Announces_Changes()
    {
        var log = Create();

        await log.RecordAsync([Outcome("first")]);
        await log.ClearAsync();

        await _bus.Received(2).PublishAsync(Arg.Any<ReceivedSpansChanged>());
    }

    [Fact(DisplayName = "An empty batch is not a change worth announcing")]
    public async Task Ignores_An_Empty_Batch()
    {
        var log = Create();

        await log.RecordAsync([]);

        await _bus.DidNotReceive().PublishAsync(Arg.Any<ReceivedSpansChanged>());
    }
}
