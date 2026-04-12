using FluentAssertions;
using Iris.Components.CommandPalette;
using Iris.Components.Settings;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using NSubstitute;

namespace Iris.Components.Test.CommandPalette;

public class NavigationPaletteCommandProviderTests
{
    [Fact(DisplayName = "Returns the seven expected step-1 commands in stable order")]
    public async Task Returns_seven_expected_commands()
    {
        var sut = NewProvider();

        var commands = await sut.GetCommandsAsync(CancellationToken.None);

        commands.Select(c => c.Id).Should().Equal(
            "nav.home",
            "nav.messaging",
            "nav.endpoints",
            "nav.packages",
            "nav.connections",
            "nav.history",
            "settings.open");
    }

    [Fact(DisplayName = "Navigation commands are grouped under 'Navigation'")]
    public async Task Navigation_commands_are_grouped()
    {
        var sut = NewProvider();

        var commands = await sut.GetCommandsAsync(CancellationToken.None);

        commands.Where(c => c.Id.StartsWith("nav.")).Should()
            .OnlyContain(c => c.Group == "Navigation");
    }

    [Fact(DisplayName = "Settings command is grouped under 'Settings'")]
    public async Task Settings_command_is_grouped()
    {
        var sut = NewProvider();

        var commands = await sut.GetCommandsAsync(CancellationToken.None);

        commands.Single(c => c.Id == "settings.open").Group.Should().Be("Settings");
    }

    [Theory(DisplayName = "Each navigation command navigates to its expected route when invoked")]
    [InlineData("nav.home", "/")]
    [InlineData("nav.messaging", "/Messaging")]
    [InlineData("nav.endpoints", "/Endpoints")]
    [InlineData("nav.packages", "/Packages")]
    [InlineData("nav.connections", "/Connections")]
    [InlineData("nav.history", "/History")]
    public async Task Navigation_commands_navigate_to_expected_routes(string id, string expectedPath)
    {
        var nav = new RecordingNavigationManager();
        var sut = NewProvider(nav);

        var commands = await sut.GetCommandsAsync(CancellationToken.None);
        var command = commands.Single(c => c.Id == id);
        await command.InvokeAsync(CancellationToken.None);

        nav.LastNavigatedPath.Should().Be(expectedPath);
    }

    [Fact(DisplayName = "Settings command opens SettingsDialog via IDialogService")]
    public async Task Settings_command_opens_settings_dialog()
    {
        var dialogService = Substitute.For<IDialogService>();
        var sut = NewProvider(dialogService: dialogService);

        var commands = await sut.GetCommandsAsync(CancellationToken.None);
        var settings = commands.Single(c => c.Id == "settings.open");
        await settings.InvokeAsync(CancellationToken.None);

        await dialogService.Received(1).ShowAsync(
            typeof(SettingsDialog),
            "Settings",
            Arg.Any<DialogOptions>());
    }

    [Fact(DisplayName = "Every command has a non-empty Title and Icon")]
    public async Task Every_command_has_title_and_icon()
    {
        var sut = NewProvider();

        var commands = await sut.GetCommandsAsync(CancellationToken.None);

        commands.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Title));
        commands.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Icon));
    }

    // ----- helpers -----

    private static NavigationPaletteCommandProvider NewProvider(
        NavigationManager? navigationManager = null,
        IDialogService? dialogService = null) =>
        new(
            navigationManager ?? new RecordingNavigationManager(),
            dialogService ?? Substitute.For<IDialogService>());

    private sealed class RecordingNavigationManager : NavigationManager
    {
        public RecordingNavigationManager()
        {
            Initialize("https://localhost/", "https://localhost/");
        }

        public string? LastNavigatedPath { get; private set; }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            LastNavigatedPath = uri;
        }
    }
}
