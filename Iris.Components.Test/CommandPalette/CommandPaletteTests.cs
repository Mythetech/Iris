using Bunit;
using FluentAssertions;
using Iris.Components.CommandPalette;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Iris.Components.Test.CommandPalette;

public class CommandPaletteTests : IrisTestContext
{
    private readonly List<string> _invokedIds = new();
    private int _cancelCount;
    private int _invokedCount;

    public CommandPaletteTests()
    {
        var provider = new TestCommandProvider(
            Cmd("home", "Home"),
            Cmd("messaging", "Messaging", description: "Compose and send messages",
                keywords: new[] { "send", "compose" }),
            Cmd("history", "History"));

        Services.AddSingleton(new CommandPaletteService(
            new ICommandProvider[] { provider },
            NullLogger<CommandPaletteService>.Instance));
    }

    [Fact(DisplayName = "Renders all commands when query is empty")]
    public void Renders_all_commands_initially()
    {
        var cut = RenderPalette();

        var options = cut.FindAll("[role='option']");
        options.Should().HaveCount(3);
        options.Select(o => o.GetAttribute("data-command-id")).Should().Equal("home", "messaging", "history");
    }

    [Fact(DisplayName = "Initial selected index is 0 and aria-activedescendant points to first command")]
    public void Initial_selection_is_first_command()
    {
        var cut = RenderPalette();

        var input = cut.Find("input");
        input.GetAttribute("aria-activedescendant").Should().Be("cmd-home");

        var options = cut.FindAll("[role='option']");
        options[0].ClassList.Should().Contain("iris-cmd-palette-row--selected");
    }

    [Fact(DisplayName = "Typing in the search input filters the results")]
    public async Task Typing_filters_results()
    {
        var cut = RenderPalette();

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "mess" });

        var options = cut.FindAll("[role='option']");
        options.Should().HaveCount(1);
        options[0].GetAttribute("data-command-id").Should().Be("messaging");
    }

    [Fact(DisplayName = "Filter matches description text via keyword")]
    public async Task Filter_matches_description_through_service()
    {
        var cut = RenderPalette();

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "compose" });

        var options = cut.FindAll("[role='option']");
        options.Select(o => o.GetAttribute("data-command-id")).Should().Equal("messaging");
    }

    [Fact(DisplayName = "ArrowDown moves selection forward and wraps at end")]
    public async Task ArrowDown_wraps_at_end()
    {
        var cut = RenderPalette();
        var input = cut.Find("input");

        await input.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        input.GetAttribute("aria-activedescendant").Should().Be("cmd-messaging");

        await input.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        input.GetAttribute("aria-activedescendant").Should().Be("cmd-history");

        await input.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        input.GetAttribute("aria-activedescendant").Should().Be("cmd-home");
    }

    [Fact(DisplayName = "ArrowUp moves selection backward and wraps at start")]
    public async Task ArrowUp_wraps_at_start()
    {
        var cut = RenderPalette();
        var input = cut.Find("input");

        await input.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });
        input.GetAttribute("aria-activedescendant").Should().Be("cmd-history");

        await input.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });
        input.GetAttribute("aria-activedescendant").Should().Be("cmd-messaging");
    }

    [Fact(DisplayName = "Home key jumps selection to first command")]
    public async Task Home_jumps_to_first()
    {
        var cut = RenderPalette();
        var input = cut.Find("input");

        await input.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Home" });

        input.GetAttribute("aria-activedescendant").Should().Be("cmd-home");
    }

    [Fact(DisplayName = "End key jumps selection to last command")]
    public async Task End_jumps_to_last()
    {
        var cut = RenderPalette();
        var input = cut.Find("input");

        await input.KeyDownAsync(new KeyboardEventArgs { Key = "End" });

        input.GetAttribute("aria-activedescendant").Should().Be("cmd-history");
    }

    [Fact(DisplayName = "Enter invokes the selected command and fires OnInvoked")]
    public async Task Enter_invokes_selected_command_and_fires_callback()
    {
        var cut = RenderPalette();
        var input = cut.Find("input");

        await input.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" }); // select messaging
        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        _invokedIds.Should().Equal("messaging");
        _invokedCount.Should().Be(1);
        _cancelCount.Should().Be(0);
    }

    [Fact(DisplayName = "Escape fires OnCancel without invoking any command")]
    public async Task Escape_cancels_without_invoking()
    {
        var cut = RenderPalette();

        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        _invokedIds.Should().BeEmpty();
        _cancelCount.Should().Be(1);
        _invokedCount.Should().Be(0);
    }

    [Fact(DisplayName = "Selection resets to index 0 when filter results change")]
    public async Task Selection_resets_when_filter_changes()
    {
        var cut = RenderPalette();
        var input = cut.Find("input");

        await input.KeyDownAsync(new KeyboardEventArgs { Key = "End" }); // select history
        input.GetAttribute("aria-activedescendant").Should().Be("cmd-history");

        // typing should reset selection to first matching command
        await input.InputAsync(new ChangeEventArgs { Value = "h" });

        // both home and history match, so selection should land on home (index 0 of filtered)
        input.GetAttribute("aria-activedescendant").Should().Be("cmd-home");
    }

    [Fact(DisplayName = "Empty results render the empty-state message and Enter is a no-op")]
    public async Task Empty_state_renders_and_enter_is_noop()
    {
        var cut = RenderPalette();
        var input = cut.Find("input");

        await input.InputAsync(new ChangeEventArgs { Value = "zzznotacommand" });

        cut.FindAll("[role='option']").Should().BeEmpty();
        cut.Markup.Should().Contain("No commands match");

        await input.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        _invokedIds.Should().BeEmpty();
        _invokedCount.Should().Be(0);
        _cancelCount.Should().Be(0);
    }

    [Fact(DisplayName = "Mouse hover over a row updates selection to that row")]
    public void Hover_updates_selection()
    {
        var cut = RenderPalette();

        var historyRow = cut.FindAll("[role='option']")[2];
        historyRow.MouseOver();

        cut.Find("input").GetAttribute("aria-activedescendant").Should().Be("cmd-history");
    }

    [Fact(DisplayName = "Click on a row invokes that command and fires OnInvoked")]
    public async Task Click_invokes_command_and_fires_callback()
    {
        var cut = RenderPalette();

        await cut.FindAll("[role='option']")[1].ClickAsync(new MouseEventArgs());

        _invokedIds.Should().Equal("messaging");
        _invokedCount.Should().Be(1);
    }

    // ----- helpers -----

    private IRenderedComponent<CommandPalettePanel> RenderPalette() =>
        Render<CommandPalettePanel>(parameters => parameters
            .Add(p => p.OnCancel, EventCallback.Factory.Create(this, () => _cancelCount++))
            .Add(p => p.OnInvoked, EventCallback.Factory.Create<string>(this, _ => _invokedCount++)));

    private PaletteCommand Cmd(
        string id,
        string title,
        string? description = null,
        IReadOnlyList<string>? keywords = null) =>
        new(
            Id: id,
            Title: title,
            Description: description,
            Icon: "icon",
            Keywords: keywords ?? Array.Empty<string>(),
            InvokeAsync: ct =>
            {
                _invokedIds.Add(id);
                return Task.CompletedTask;
            });

    private sealed class TestCommandProvider : ICommandProvider
    {
        private readonly IReadOnlyList<PaletteCommand> _commands;
        public TestCommandProvider(params PaletteCommand[] commands) => _commands = commands;
        public ValueTask<IReadOnlyList<PaletteCommand>> GetCommandsAsync(CancellationToken ct)
            => ValueTask.FromResult(_commands);
    }
}
