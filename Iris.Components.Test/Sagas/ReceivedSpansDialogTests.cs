using Bunit;
using FluentAssertions;
using Iris.Components.Sagas;
using Iris.Sagas;
using Iris.Telemetry;
using Iris.Telemetry.Messages;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;

namespace Iris.Components.Test.Sagas;

public class ReceivedSpansDialogTests : IrisTestContext
{
    public ReceivedSpansDialogTests()
    {
        Services.AddMessageBus(typeof(ReceivedSpanLog).Assembly);
        Services.AddSingleton(Substitute.For<ISagaDefinitionProvider>());
        Services.AddSingleton<ISagaSpanMapper, StubSpanMapper>();
        Services.AddSingleton<SagaDefinitionState>();
        Services.AddSingleton<SagaInstanceState>();
        Services.AddSingleton<ReceivedSpanLog>();
        Services.UseMessageBus(typeof(ReceivedSpanLog).Assembly);
        RenderComponent<MudPopoverProvider>();
    }

    private ReceivedSpanLog Log => Services.GetRequiredService<ReceivedSpanLog>();

    private static ReceivedSpan Plain(string name = "GET /orders")
    {
        var at = DateTimeOffset.UtcNow;
        return new ReceivedSpan("trace", Guid.NewGuid().ToString("N")[..16], null, name, "svc", at, at,
            new Dictionary<string, string> { ["http.method"] = "GET" });
    }

    // MudDialog renders through the provider rather than in place, so the dialog only produces markup
    // when it is shown the way the chip shows it.
    private async Task<IRenderedComponent<MudDialogProvider>> ShowDialogAsync()
    {
        var provider = RenderComponent<MudDialogProvider>();
        var dialogs = Services.GetRequiredService<IDialogService>();
        await provider.InvokeAsync(() => dialogs.ShowAsync(typeof(ReceivedSpansDialog), "Received spans"));
        return provider;
    }

    [Fact(DisplayName = "Lists a span Iris made nothing of, which no other view can show")]
    public async Task Lists_A_Span_That_Mapped_To_Nothing()
    {
        await Log.RecordAsync([new SpanIngestOutcome(Plain(), SpanIngest.NotASagaSpan)]);

        var provider = await ShowDialogAsync();

        provider.Markup.Should().Contain("GET /orders");
        provider.FindAll("tr.span-row").Should().ContainSingle();
    }

    [Fact(DisplayName = "Marks each span with what ingest made of it")]
    public async Task Marks_How_Each_Span_Was_Classified()
    {
        await Log.RecordAsync([
            new SpanIngestOutcome(Plain("plain"), SpanIngest.NotASagaSpan),
            new SpanIngestOutcome(Plain("ghost"), SpanIngest.Unmatched),
            new SpanIngestOutcome(Plain("real"), SpanIngest.Mapped),
        ]);

        var provider = await ShowDialogAsync();

        provider.FindAll("tr.span-row .span-result").Select(e => e.TextContent.Trim())
            .Should().Equal("Mapped", "Unmatched", "No saga tags");
    }

    [Fact(DisplayName = "Says the table is a window when more spans arrived than it keeps")]
    public async Task Admits_The_Window_Is_Bounded()
    {
        var overflow = ReceivedSpanLog.MaxSpans + 5;
        await Log.RecordAsync(Enumerable.Range(0, overflow)
            .Select(_ => new SpanIngestOutcome(Plain(), SpanIngest.NotASagaSpan)).ToList());

        var provider = await ShowDialogAsync();

        provider.Markup.Should().Contain($"The {ReceivedSpanLog.MaxSpans} most recent of {overflow} spans");
    }

    [Fact(DisplayName = "An open dialog follows spans as they arrive, which is the whole point when nothing is showing up")]
    public async Task Follows_New_Spans_While_Open()
    {
        var provider = await ShowDialogAsync();
        provider.FindAll("tr.span-row").Should().BeEmpty();

        await Services.GetRequiredService<IMessageBus>().PublishAsync(new SpanBatchReceived([Plain("late arrival")]));

        provider.Markup.Should().Contain("late arrival",
            "someone opens this because nothing is appearing, so it has to show the span that finally does");
    }

    [Fact(DisplayName = "Expanding a row shows the span's tags")]
    public async Task Expanding_Shows_The_Tags()
    {
        await Log.RecordAsync([new SpanIngestOutcome(Plain(), SpanIngest.NotASagaSpan)]);
        var provider = await ShowDialogAsync();

        await provider.Find("tr.span-row button.span-toggle").ClickAsync(new MouseEventArgs());

        provider.FindAll(".span-detail").Should().ContainSingle();
        provider.Markup.Should().Contain("http.method");
    }

    [Fact(DisplayName = "Clearing resets the spans and the instances built from them, so the counts cannot disagree")]
    public async Task Clearing_Resets_Everything_Telemetry_Built()
    {
        var instances = Services.GetRequiredService<SagaInstanceState>();
        await Services.GetRequiredService<IMessageBus>()
            .PublishAsync(new SpanBatchReceived([StubSpanMapper.Span(Guid.NewGuid(), "Nowhere", "Elsewhere"), Plain()]));
        instances.SpansReceived.Should().Be(2);
        var provider = await ShowDialogAsync();

        await provider.Find("button.span-clear").ClickAsync(new MouseEventArgs());

        Log.Recent.Should().BeEmpty();
        Log.Received.Should().Be(0);
        provider.FindAll("tr.span-row").Should().BeEmpty();
        instances.SpansReceived.Should().Be(0,
            "the chip reads its counts off the instance state, so leaving it alone would show spans the feed says never arrived");
        instances.UnmatchedCount.Should().Be(0);
    }
}
