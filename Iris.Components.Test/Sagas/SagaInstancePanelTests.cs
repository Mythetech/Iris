using Bunit;
using FluentAssertions;
using Iris.Components.Sagas;
using Iris.Sagas;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace Iris.Components.Test.Sagas;

public class SagaInstancePanelTests : IrisTestContext
{
    private static readonly Guid SagaId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    public SagaInstancePanelTests()
    {
        Render<MudPopoverProvider>();
    }

    private static SagaTransition Transition(string from, string to, int second, bool withSpan = true)
    {
        var at = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000 + second);
        var span = new Iris.Telemetry.ReceivedSpan(
            "0102030405060708090a0b0c0d0e0f10", $"span{second}", null, $"consume {to}", "sample", at, at,
            new Dictionary<string, string> { ["messaging.masstransit.end_state"] = to });
        return new SagaTransition(null, SagaId, from, to, to, span.TraceId, span.SpanId, at, withSpan ? span : null);
    }

    private static SagaInstance Instance(params SagaTransition[] transitions)
        => new(SagaId, "Sample.OrderStateMachine", transitions[^1].EndState,
            transitions[0].Timestamp, transitions[^1].Timestamp, transitions);

    private IRenderedComponent<SagaInstancePanel> RenderPanel(SagaInstance instance)
        => Render<SagaInstancePanel>(p => p
            .Add(x => x.Instances, [instance])
            .Add(x => x.Selected, instance));

    [Fact(DisplayName = "Timeline rows start collapsed so the timeline stays scannable")]
    public void Rows_Start_Collapsed()
    {
        var cut = RenderPanel(Instance(Transition("Initial", "Submitted", 1), Transition("Submitted", "Accepted", 2)));

        cut.FindAll("button.span-toggle").Should().HaveCount(2);
        cut.FindAll(".span-detail").Should().BeEmpty();
    }

    [Fact(DisplayName = "Expanding a row shows the span that transition was read from")]
    public async Task Expanding_Shows_The_Span()
    {
        var cut = RenderPanel(Instance(Transition("Initial", "Submitted", 1)));

        await cut.Find("button.span-toggle").ClickAsync(new MouseEventArgs());

        cut.FindAll(".span-detail").Should().ContainSingle();
        cut.Markup.Should().Contain("messaging.masstransit.end_state").And.Contain("consume Submitted");
    }

    [Fact(DisplayName = "Expanding a second row closes the first, so the panel never fills with detail")]
    public async Task Only_One_Row_Is_Open()
    {
        var cut = RenderPanel(Instance(Transition("Initial", "Submitted", 1), Transition("Submitted", "Accepted", 2)));

        await cut.FindAll("button.span-toggle")[0].ClickAsync(new MouseEventArgs());
        await cut.FindAll("button.span-toggle")[1].ClickAsync(new MouseEventArgs());

        cut.FindAll(".span-detail").Should().ContainSingle();
    }

    [Fact(DisplayName = "Clicking an open row closes it again")]
    public async Task Toggling_Closes()
    {
        var cut = RenderPanel(Instance(Transition("Initial", "Submitted", 1)));

        await cut.Find("button.span-toggle").ClickAsync(new MouseEventArgs());
        await cut.Find("button.span-toggle").ClickAsync(new MouseEventArgs());

        cut.FindAll(".span-detail").Should().BeEmpty();
    }

    [Fact(DisplayName = "A transition whose span was dropped says so rather than offering nothing")]
    public async Task Explains_A_Dropped_Span()
    {
        var cut = RenderPanel(Instance(Transition("Initial", "Submitted", 1, withSpan: false)));

        await cut.Find("button.span-toggle").ClickAsync(new MouseEventArgs());

        cut.FindAll(".span-detail").Should().BeEmpty();
        cut.Markup.Should().Contain("no longer retained");
    }

    [Fact(DisplayName = "Switching instances closes any open row rather than opening one on the new timeline")]
    public async Task Selection_Change_Closes_The_Open_Row()
    {
        var first = Instance(Transition("Initial", "Submitted", 1));
        var second = Instance(Transition("Initial", "Cancelled", 5));
        var cut = RenderPanel(first);
        await cut.Find("button.span-toggle").ClickAsync(new MouseEventArgs());

        await cut.InvokeAsync(() => cut.Render(p => p
            .Add(x => x.Instances, [second])
            .Add(x => x.Selected, second)));

        cut.FindAll(".span-detail").Should().BeEmpty(
            "an index into the old timeline means nothing on the new one");
    }
}
