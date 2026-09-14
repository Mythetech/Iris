using FluentAssertions;
using Iris.Components.History;
using Iris.Contracts.Audit.Models;
using NSubstitute;

namespace Iris.Components.Test.History;

/// <summary>
/// The cached history window: what it asks the store for, how audit rows become the records
/// the grid renders, and what happens when a send lands while something is reading.
///
/// <para>
/// That last one is the reason this file exists. <c>MessageRecorder</c> adds to this from the
/// send path, and the History grid and the Recent tab enumerate it while rendering, so the
/// list is genuinely shared between a writer and readers.
/// </para>
/// </summary>
public class HistoryStateTests
{
    private static AuditRecord Audit(string action, string target = "orders", Dictionary<string, string>? details = null) =>
        new()
        {
            Action = action,
            Target = target,
            User = "Local",
            When = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero),
            Details = details ?? new Dictionary<string, string> { ["Message"] = "{}" },
        };

    private static HistoryRecord Record(string action) =>
        new() { Action = action, Source = "Local", Target = "orders" };

    private static (HistoryState State, IHistoryService Service) Create(params AuditRecord[] stored)
    {
        var service = Substitute.For<IHistoryService>();
        service.GetUserHistoryAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(_ => stored.ToList());
        return (new HistoryState(service), service);
    }

    [Fact(DisplayName = "An audit row becomes a history record with every field carried over")]
    public async Task Audit_rows_are_mapped()
    {
        var audit = Audit("MessageSent", details: new Dictionary<string, string> { ["Address"] = "amqp://local" });
        var (state, _) = Create(audit);

        await state.GetUserHistoryAsync();

        var record = state.History.Should().ContainSingle().Subject;
        record.Action.Should().Be("MessageSent");
        record.EventAction.Should().Be("MessageSent");
        record.Source.Should().Be("Local");
        record.Target.Should().Be("orders");
        record.Timestamp.Should().Be(audit.When!.Value);
        record.Details.Should().Be("""{"Address":"amqp://local"}""");
        record.EventParameters.Should().ContainKey("Address").WhoseValue.Should().Be("amqp://local");
    }

    [Fact(DisplayName = "An audit row with no details is rendered rather than crashing the page")]
    public async Task Audit_rows_without_details_are_mapped()
    {
        // Details is optional on the contract and the local service can produce a null one
        // from a row whose stored details deserialize to null. Flattening it unguarded took
        // out the whole history page, not just the row.
        var audit = Audit("MessageSent");
        audit.Details = null;
        var (state, _) = Create(audit);

        await state.GetUserHistoryAsync();

        var record = state.History.Should().ContainSingle().Subject;
        record.EventParameters.Should().BeEmpty();
    }

    [Fact(DisplayName = "The page and page size asked for are the ones the store is given")]
    public async Task Paging_is_passed_through()
    {
        var (state, service) = Create();

        await state.GetUserHistoryAsync(page: 3, pageSize: 10);

        await service.Received(1).GetUserHistoryAsync(3, 10);
    }

    [Fact(DisplayName = "The default window is the cache ceiling, not a storage page size")]
    public async Task Default_page_size_is_the_window()
    {
        var (state, service) = Create();

        await state.GetUserHistoryAsync();

        await service.Received(1).GetUserHistoryAsync(1, HistoryState.DefaultPageSize);
    }

    [Fact(DisplayName = "A loaded window is served from the cache")]
    public async Task Loaded_history_is_cached()
    {
        var (state, service) = Create(Audit("MessageSent"));

        await state.GetUserHistoryAsync();
        await state.GetUserHistoryAsync();

        await service.Received(1).GetUserHistoryAsync(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact(DisplayName = "An empty window is retried rather than cached as empty")]
    public async Task An_empty_window_is_retried()
    {
        var (state, service) = Create();

        await state.GetUserHistoryAsync();
        await state.GetUserHistoryAsync();

        await service.Received(2).GetUserHistoryAsync(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact(DisplayName = "Refreshing drops the cache, announces it, and reloads on the next read")]
    public async Task Refresh_drops_the_cache()
    {
        var (state, service) = Create(Audit("MessageSent"));
        await state.GetUserHistoryAsync();
        var notified = 0;
        state.OnHistoryStateChange += () => notified++;

        state.Refresh();

        state.History.Should().BeNull();
        notified.Should().Be(1, "clearing history from the settings panel left both the grid and the Recent tab showing deleted records");

        await state.GetUserHistoryAsync();
        await service.Received(2).GetUserHistoryAsync(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact(DisplayName = "A sent message is appended and announced")]
    public async Task Adding_appends_and_announces()
    {
        var (state, _) = Create(Audit("First"));
        await state.GetUserHistoryAsync();
        var notified = 0;
        state.OnHistoryStateChange += () => notified++;

        await state.AddHistoryRecord(Record("Second"));

        state.History!.Select(r => r.Action).Should().Equal("First", "Second");
        notified.Should().Be(1);
    }

    [Fact(DisplayName = "The first send of a session loads the window before appending to it")]
    public async Task Adding_loads_the_window_first()
    {
        var (state, service) = Create(Audit("First"));

        await state.AddHistoryRecord(Record("Second"));

        await service.Received(1).GetUserHistoryAsync(Arg.Any<int>(), Arg.Any<int>());
        state.History!.Select(r => r.Action).Should().Equal("First", "Second");
    }

    [Fact(DisplayName = "A send landing mid-render does not disturb the list being rendered")]
    public async Task Adding_does_not_mutate_a_list_in_use()
    {
        // MessageRecorder adds from the send path while the History grid and the Recent tab
        // enumerate the same list, with no lock and no snapshot between them. Adding in
        // place is an InvalidOperationException in whichever of them is mid-render.
        var (state, _) = Create(Audit("First"), Audit("Second"));
        await state.GetUserHistoryAsync();

        var rendering = state.History!;
        using var reader = rendering.GetEnumerator();
        reader.MoveNext();

        await state.AddHistoryRecord(Record("Third"));

        var act = () => reader.MoveNext();
        act.Should().NotThrow<InvalidOperationException>();
        rendering.Should().HaveCount(2, "the reader's list is finished with, and a new one carries the addition");
        state.History!.Should().HaveCount(3);
    }

    [Fact(DisplayName = "Concurrent sends all land")]
    public async Task Concurrent_adds_all_land()
    {
        var (state, _) = Create(Audit("First"));
        await state.GetUserHistoryAsync();

        await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(i => Task.Run(() => state.AddHistoryRecord(Record($"Send {i}")), TestContext.Current.CancellationToken)));

        state.History!.Should().HaveCount(51);
    }
}
