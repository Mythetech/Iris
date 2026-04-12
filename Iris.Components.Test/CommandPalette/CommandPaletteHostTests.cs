using Bunit;
using FluentAssertions;
using Iris.Components.CommandPalette;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Utilities;
using NSubstitute;

namespace Iris.Components.Test.CommandPalette;

public class CommandPaletteHostTests : IrisTestContext
{
    private readonly IDialogService _dialogService = Substitute.For<IDialogService>();
    private readonly CommandPaletteService _paletteService;

    public CommandPaletteHostTests()
    {
        _paletteService = new CommandPaletteService(
            Array.Empty<ICommandProvider>(),
            NullLogger<CommandPaletteService>.Instance);
        Services.AddSingleton(_paletteService);
        Services.AddSingleton(_dialogService);
    }

    [Fact(DisplayName = "Mounts two MudHotkey instances: Ctrl+K and Cmd+K")]
    public void Mounts_two_hotkeys_for_ctrl_and_cmd()
    {
        var cut = RenderComponent<CommandPaletteHost>();

        var hotkeys = cut.FindComponents<MudHotkey>();
        hotkeys.Should().HaveCount(2);

        var modifiers = hotkeys
            .Select(h => h.Instance.KeyModifiers.Single())
            .ToArray();

        modifiers.Should().Contain(JsKeyModifier.ControlLeft);
        modifiers.Should().Contain(JsKeyModifier.MetaLeft);

        hotkeys.Should().OnlyContain(h => h.Instance.Key == JsKey.KeyK);
    }

    [Fact(DisplayName = "Pressing the hotkey opens the CommandPaletteDialog via IDialogService")]
    public async Task Hotkey_opens_command_palette_dialog()
    {
        var cut = RenderComponent<CommandPaletteHost>();
        var ctrlHotkey = cut.FindComponents<MudHotkey>()
            .Single(h => h.Instance.KeyModifiers.Contains(JsKeyModifier.ControlLeft));

        await cut.InvokeAsync(() => ctrlHotkey.Instance.MudHotkeyProviderJsCallback());

        await _dialogService.Received(1).ShowAsync(
            typeof(CommandPaletteDialog),
            Arg.Any<string>(),
            Arg.Any<DialogOptions>());
    }

    [Fact(DisplayName = "Hotkey is a no-op when palette is already open")]
    public async Task Hotkey_does_not_stack_when_palette_already_open()
    {
        _paletteService.MarkOpened();
        var cut = RenderComponent<CommandPaletteHost>();
        var ctrlHotkey = cut.FindComponents<MudHotkey>()
            .Single(h => h.Instance.KeyModifiers.Contains(JsKeyModifier.ControlLeft));

        await cut.InvokeAsync(() => ctrlHotkey.Instance.MudHotkeyProviderJsCallback());

        await _dialogService.DidNotReceive().ShowAsync(
            typeof(CommandPaletteDialog),
            Arg.Any<string>(),
            Arg.Any<DialogOptions>());
    }

    [Fact(DisplayName = "Both Ctrl+K and Cmd+K hotkeys open the palette")]
    public async Task Both_hotkeys_open_palette()
    {
        var cut = RenderComponent<CommandPaletteHost>();
        var hotkeys = cut.FindComponents<MudHotkey>().ToArray();

        // First press: Ctrl+K. Then mark closed (simulating dialog close) and press Cmd+K.
        await cut.InvokeAsync(() => hotkeys[0].Instance.MudHotkeyProviderJsCallback());
        _paletteService.MarkClosed();
        await cut.InvokeAsync(() => hotkeys[1].Instance.MudHotkeyProviderJsCallback());

        await _dialogService.Received(2).ShowAsync(
            typeof(CommandPaletteDialog),
            Arg.Any<string>(),
            Arg.Any<DialogOptions>());
    }
}
