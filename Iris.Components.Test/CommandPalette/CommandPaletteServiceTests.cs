using FluentAssertions;
using Iris.Components.CommandPalette;
using Microsoft.Extensions.Logging.Abstractions;

namespace Iris.Components.Test.CommandPalette;

public class CommandPaletteServiceTests
{
    [Fact(DisplayName = "Empty query returns all commands from all providers in provider order")]
    public async Task Empty_query_returns_all_commands_in_provider_order()
    {
        var p1 = new FakeCommandProvider(Cmd("a", "Apple"), Cmd("b", "Banana"));
        var p2 = new FakeCommandProvider(Cmd("c", "Cherry"));
        var sut = NewService(p1, p2);

        var result = await sut.GetCommandsAsync(query: string.Empty, CancellationToken.None);

        result.Commands.Select(c => c.Id).Should().Equal("a", "b", "c");
        result.HasProviderErrors.Should().BeFalse();
    }

    [Fact(DisplayName = "Filter is case-insensitive on Title")]
    public async Task Filter_is_case_insensitive_on_title()
    {
        var sut = NewService(new FakeCommandProvider(
            Cmd("a", "Messaging"),
            Cmd("b", "History")));

        var result = await sut.GetCommandsAsync("MESS", CancellationToken.None);

        result.Commands.Select(c => c.Id).Should().Equal("a");
    }

    [Fact(DisplayName = "Filter matches on Description")]
    public async Task Filter_matches_on_description()
    {
        var sut = NewService(new FakeCommandProvider(
            Cmd("a", "Messaging", description: "Compose and send messages"),
            Cmd("b", "History")));

        var result = await sut.GetCommandsAsync("compose", CancellationToken.None);

        result.Commands.Select(c => c.Id).Should().Equal("a");
    }

    [Fact(DisplayName = "Filter matches on Keywords")]
    public async Task Filter_matches_on_keywords()
    {
        var sut = NewService(new FakeCommandProvider(
            Cmd("a", "Messaging", keywords: new[] { "send", "publish" }),
            Cmd("b", "History")));

        var result = await sut.GetCommandsAsync("publish", CancellationToken.None);

        result.Commands.Select(c => c.Id).Should().Equal("a");
    }

    [Fact(DisplayName = "Ranking: title prefix beats title substring beats keyword match")]
    public async Task Ranking_title_prefix_first_then_substring_then_keyword()
    {
        var sut = NewService(new FakeCommandProvider(
            // keyword-only match
            Cmd("kw", "Settings", keywords: new[] { "messaging" }),
            // substring (not prefix) match in title
            Cmd("sub", "User Messaging Center"),
            // prefix match in title
            Cmd("pre", "Messaging")));

        var result = await sut.GetCommandsAsync("mess", CancellationToken.None);

        result.Commands.Select(c => c.Id).Should().Equal("pre", "sub", "kw");
    }

    [Fact(DisplayName = "Provider that throws is excluded; other providers still return; HasProviderErrors set")]
    public async Task Throwing_provider_is_isolated()
    {
        var good = new FakeCommandProvider(Cmd("a", "Apple"));
        var bad = new ThrowingCommandProvider();
        var sut = NewService(good, bad);

        var result = await sut.GetCommandsAsync(string.Empty, CancellationToken.None);

        result.Commands.Select(c => c.Id).Should().Equal("a");
        result.HasProviderErrors.Should().BeTrue();
    }

    [Fact(DisplayName = "Whitespace-only query is treated as empty")]
    public async Task Whitespace_query_is_treated_as_empty()
    {
        var sut = NewService(new FakeCommandProvider(
            Cmd("a", "Apple"),
            Cmd("b", "Banana")));

        var result = await sut.GetCommandsAsync("   ", CancellationToken.None);

        result.Commands.Select(c => c.Id).Should().Equal("a", "b");
    }

    [Fact(DisplayName = "IsOpen flag round-trips via Open and Close")]
    public void IsOpen_flag_round_trips()
    {
        var sut = NewService();

        sut.IsOpen.Should().BeFalse();
        sut.MarkOpened();
        sut.IsOpen.Should().BeTrue();
        sut.MarkClosed();
        sut.IsOpen.Should().BeFalse();
    }

    // ----- helpers -----

    private static CommandPaletteService NewService(params ICommandProvider[] providers) =>
        new(providers, NullLogger<CommandPaletteService>.Instance);

    private static PaletteCommand Cmd(
        string id,
        string title,
        string? description = null,
        IReadOnlyList<string>? keywords = null,
        string? group = null) =>
        new(
            Id: id,
            Title: title,
            Description: description,
            Icon: "icon",
            Keywords: keywords ?? Array.Empty<string>(),
            InvokeAsync: _ => Task.CompletedTask,
            Group: group);

    private sealed class FakeCommandProvider : ICommandProvider
    {
        private readonly IReadOnlyList<PaletteCommand> _commands;
        public FakeCommandProvider(params PaletteCommand[] commands) => _commands = commands;
        public ValueTask<IReadOnlyList<PaletteCommand>> GetCommandsAsync(CancellationToken ct)
            => ValueTask.FromResult(_commands);
    }

    private sealed class ThrowingCommandProvider : ICommandProvider
    {
        public ValueTask<IReadOnlyList<PaletteCommand>> GetCommandsAsync(CancellationToken ct)
            => throw new InvalidOperationException("provider exploded");
    }
}
