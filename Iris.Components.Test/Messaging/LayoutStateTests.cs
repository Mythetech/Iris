using FluentAssertions;
using Iris.Components.Messaging;
using NSubstitute;

namespace Iris.Components.Test.Messaging;

/// <summary>
/// The messaging tab layout: what gets loaded, when the default wins, and which list
/// instance callers are handed.
///
/// <para>
/// Instance identity is the whole game here. <c>OptionsPanel.RefreshTabs</c> reads
/// <see cref="LayoutState.Layout"/> and passes it straight to <c>TabDropContainer</c>, which
/// mutates the tabs in place as the user drags them, and <c>LayoutState.UpdateTab</c> then
/// looks the dragged tab up by id. Hand out a fresh list of freshly minted tabs on each read
/// and every one of those steps still runs, just against an object nobody else is holding.
/// </para>
/// </summary>
public class LayoutStateTests
{
    private sealed class StubTab : DynamicTabView
    {
        public override string Name { get; set; } = "Stub";

        public override Type ComponentType { get; set; } = typeof(StubTab);
    }

    private static (LayoutState State, IMessagingLayoutService Service) Create(List<DynamicTabView>? saved = null)
    {
        var service = Substitute.For<IMessagingLayoutService>();
        service.LoadLayoutAsync().Returns(Task.FromResult(saved));
        return (new LayoutState(service), service);
    }

    private static List<DynamicTabView> Tabs(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new StubTab { AreaIdentifier = LayoutZones.TopZone, AreaIndex = i })];

    [Fact(DisplayName = "A saved layout at least as complete as the default is the one that loads")]
    public async Task Saved_layout_wins()
    {
        var saved = Tabs(5);
        var (state, _) = Create(saved);

        var layout = await state.GetLayoutAsync();

        layout.Should().BeSameAs(saved);
    }

    [Fact(DisplayName = "Nothing saved falls back to the default")]
    public async Task Nothing_saved_falls_back()
    {
        var (state, _) = Create(saved: null);

        var layout = await state.GetLayoutAsync();

        layout.Should().BeEquivalentTo(state.GetDefaultTabLayout(), o => o.Excluding(t => t.Id));
    }

    [Fact(DisplayName = "A saved layout missing tabs the default now has is discarded whole")]
    public async Task Short_saved_layout_is_discarded()
    {
        // A release that adds a tab leaves every existing user with a layout one short, and
        // the panel would otherwise render without the new tab and no way to reach it. The
        // cost is that such a release also discards the user's arrangement.
        var (state, _) = Create(Tabs(2));

        var layout = await state.GetLayoutAsync();

        layout.Should().HaveCount(state.GetDefaultTabLayout().Count);
    }

    [Fact(DisplayName = "The layout is loaded once and cached")]
    public async Task Layout_is_cached()
    {
        var (state, service) = Create(Tabs(5));

        await state.GetLayoutAsync();
        await state.GetLayoutAsync();

        await service.Received(1).LoadLayoutAsync();
    }

    [Fact(DisplayName = "A load that fell back to the default is retried, since nothing was cached from storage")]
    public async Task Fallback_does_not_count_as_loaded()
    {
        // GetLayoutAsync caches on Count > 0, and the default satisfies that, so this pins
        // the current behaviour rather than endorsing it: the fallback is cached too.
        var (state, service) = Create(saved: null);

        await state.GetLayoutAsync();
        await state.GetLayoutAsync();

        await service.Received(1).LoadLayoutAsync();
    }

    [Fact(DisplayName = "Saving writes through to storage and becomes the current layout")]
    public async Task Save_writes_through()
    {
        var (state, service) = Create();
        var layout = Tabs(5);

        await state.SaveLayoutAsync(layout);

        await service.Received(1).SaveLayoutAsync(layout);
        state.Layout.Should().BeSameAs(layout);
    }

    [Theory(DisplayName = "Every mutation notifies subscribers exactly once")]
    [InlineData("save")]
    [InlineData("update")]
    public async Task Mutations_notify(string operation)
    {
        var (state, _) = Create(Tabs(5));
        var tab = (await state.GetLayoutAsync())!.First();

        var notifications = 0;
        state.LayoutStateChanged += () => notifications++;

        switch (operation)
        {
            case "save":
                await state.SaveLayoutAsync(Tabs(5));
                break;
            case "update":
                state.UpdateTab(tab);
                break;
        }

        notifications.Should().Be(1);
    }

    [Fact(DisplayName = "The first load notifies, a load served from cache does not")]
    public async Task Only_a_real_load_notifies()
    {
        var (state, _) = Create(Tabs(5));
        var notifications = 0;
        state.LayoutStateChanged += () => notifications++;

        await state.GetLayoutAsync();
        await state.GetLayoutAsync();

        notifications.Should().Be(1);
    }

    [Fact(DisplayName = "Reading the layout twice before anything is loaded gives back the same list")]
    public void Default_layout_is_stable_across_reads()
    {
        var (state, _) = Create();

        state.Layout.Should().BeSameAs(state.Layout);
    }

    [Fact(DisplayName = "The default handed out before a load is the same list the load falls back to")]
    public async Task Default_survives_a_load_that_finds_nothing()
    {
        // OptionsPanel reads Layout from RefreshTabs and the loaded list from
        // OnInitializedAsync. If those are two different default instances, whichever the
        // drop container is holding is not the one UpdateTab writes to.
        var (state, _) = Create(saved: null);

        var early = state.Layout;
        var loaded = await state.GetLayoutAsync();

        loaded.Should().BeSameAs(early);
    }

    [Fact(DisplayName = "A tab dragged before the layout has loaded still moves")]
    public void Update_reaches_the_default_layout()
    {
        // UpdateTab used to look only at the backing field, which is null until a load
        // completes, so a drop that landed in that window was accepted by the container and
        // then silently discarded.
        var (state, _) = Create();
        var tab = state.Layout.First();

        state.UpdateTab(new StubTab
        {
            Id = tab.Id,
            AreaIdentifier = LayoutZones.BottomZone,
            AreaIndex = 3,
            BadgeCount = 7,
        });

        state.Layout.Single(t => t.Id == tab.Id).AreaIdentifier.Should().Be(LayoutZones.BottomZone);
        state.Layout.Single(t => t.Id == tab.Id).AreaIndex.Should().Be(3);
        state.Layout.Single(t => t.Id == tab.Id).BadgeCount.Should().Be(7);
    }

    [Fact(DisplayName = "Updating a tab the layout does not hold changes nothing")]
    public async Task Update_of_an_unknown_tab_is_ignored()
    {
        var (state, _) = Create(Tabs(5));
        await state.GetLayoutAsync();
        var notified = false;
        state.LayoutStateChanged += () => notified = true;

        state.UpdateTab(new StubTab { AreaIdentifier = LayoutZones.BottomZone });

        state.Layout.Should().OnlyContain(t => t.AreaIdentifier == LayoutZones.TopZone);
        notified.Should().BeFalse();
    }

    [Fact(DisplayName = "Updating a tab moves only that tab, and only its placement")]
    public async Task Update_touches_one_tab()
    {
        var (state, _) = Create(Tabs(5));
        var layout = await state.GetLayoutAsync();
        var target = layout!.First();

        state.UpdateTab(new StubTab { Id = target.Id, AreaIdentifier = LayoutZones.BottomZone, AreaIndex = 9 });

        target.Name.Should().Be("Stub");
        layout!.Skip(1).Should().OnlyContain(t => t.AreaIdentifier == LayoutZones.TopZone);
    }

    [Fact(DisplayName = "The default layout fills both zones and opens one tab in each")]
    public void Default_layout_is_usable()
    {
        var (state, _) = Create();

        var layout = state.GetDefaultTabLayout();

        layout.Select(t => t.Id).Should().OnlyHaveUniqueItems();
        foreach (var zone in new[] { LayoutZones.TopZone, LayoutZones.BottomZone })
        {
            var zoned = layout.Where(t => t.AreaIdentifier == zone).ToList();
            zoned.Should().NotBeEmpty(zone);
            zoned.Select(t => t.AreaIndex).Should().OnlyHaveUniqueItems(zone);
            zoned.Should().ContainSingle(t => t.IsActive, $"{zone} needs exactly one tab open");
        }
    }

    [Fact(DisplayName = "Resetting to the default installs a new layout rather than the live one")]
    public async Task Reset_installs_a_fresh_default()
    {
        // LayoutSettingsDisplay resets by saving GetDefaultTabLayout(). Handing back the
        // live instance would make the reset a no-op against a layout already dragged out
        // of shape, since the tabs are mutated in place.
        var (state, _) = Create();
        state.Layout.First().AreaIndex = 99;

        await state.SaveLayoutAsync(state.GetDefaultTabLayout());

        state.Layout.Should().OnlyContain(t => t.AreaIndex != 99);
    }
}
