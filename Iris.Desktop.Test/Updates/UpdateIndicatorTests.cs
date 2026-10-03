using Bunit;
using FluentAssertions;
using Iris.Components.Shared;
using Iris.Desktop.Updates;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Mythetech.Framework.Desktop.Updates;
using Mythetech.Framework.Desktop.Updates.Components;
using Mythetech.Framework.Desktop.Updates.Events;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;

namespace Iris.Desktop.Test.Updates;

public class UpdateIndicatorTests : BunitContext
{
    private readonly IUpdateService _updates = Substitute.For<IUpdateService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    public UpdateIndicatorTests()
    {
        Services.AddMudServices();
        Services.AddMessageBus();
        Services.AddSingleton(_updates);
        Services.AddSingleton(_dialogs);

        JSInterop.Mode = JSRuntimeMode.Loose;
        // An explicit Setup without a result hangs instead of falling back to Loose mode.
        JSInterop.Setup<int>("mudpopoverHelper.countProviders", _ => true).SetResult(1);
        Render<MudPopoverProvider>();
    }

    private IMessageBus Bus => Services.GetRequiredService<IMessageBus>();

    private static UpdateInfo Update(bool downloaded = false) =>
        new() { TargetVersion = new Version(1, 2, 3), IsDownloaded = downloaded };

    [Fact(DisplayName = "Nothing is shown while no update is available")]
    public void Hidden_without_an_update()
    {
        _updates.AvailableUpdate.Returns((UpdateInfo?)null);

        var cut = Render<UpdateIndicator>();

        cut.FindComponents<IconButton>().Should().BeEmpty();
    }

    [Fact(DisplayName = "An update found before the indicator rendered is still shown")]
    public void Shows_an_update_that_was_already_available()
    {
        _updates.AvailableUpdate.Returns(Update());

        var cut = Render<UpdateIndicator>();

        cut.FindComponent<IconButton>().Instance.Title.Should().Be("Update available: v1.2.3");
    }

    [Fact(DisplayName = "A downloaded update prompts for a restart instead")]
    public void Prompts_for_a_restart_once_downloaded()
    {
        _updates.AvailableUpdate.Returns(Update(downloaded: true));

        var cut = Render<UpdateIndicator>();

        cut.FindComponent<IconButton>().Instance.Title.Should().Be("Restart to update to v1.2.3");
    }

    [Fact(DisplayName = "A check that finds an update shows the indicator")]
    public async Task Appears_when_a_check_finds_an_update()
    {
        _updates.AvailableUpdate.Returns((UpdateInfo?)null);
        var cut = Render<UpdateIndicator>();

        var update = Update();
        _updates.AvailableUpdate.Returns(update);
        await Bus.PublishAsync(new UpdateCheckCompleted(update));

        cut.WaitForAssertion(() =>
            cut.FindComponent<IconButton>().Instance.Title.Should().Be("Update available: v1.2.3"));
    }

    [Fact(DisplayName = "A later check that finds nothing hides the indicator")]
    public async Task Disappears_when_a_later_check_finds_nothing()
    {
        _updates.AvailableUpdate.Returns(Update());
        var cut = Render<UpdateIndicator>();

        _updates.AvailableUpdate.Returns((UpdateInfo?)null);
        await Bus.PublishAsync(new UpdateCheckCompleted(null));

        cut.WaitForAssertion(() => cut.FindComponents<IconButton>().Should().BeEmpty());
    }

    [Fact(DisplayName = "A failed check leaves an update that was already found on screen")]
    public async Task Stays_when_a_later_check_fails()
    {
        _updates.AvailableUpdate.Returns(Update());
        var cut = Render<UpdateIndicator>();

        await Bus.PublishAsync(new UpdateCheckCompleted(null));

        cut.WaitForAssertion(() =>
            cut.FindComponent<IconButton>().Instance.Title.Should().Be("Update available: v1.2.3"));
    }

    [Fact(DisplayName = "Finishing the download switches the indicator to the restart prompt")]
    public async Task Switches_to_the_restart_prompt_when_the_download_finishes()
    {
        var update = Update();
        _updates.AvailableUpdate.Returns(update);
        var cut = Render<UpdateIndicator>();

        update.IsDownloaded = true;
        await Bus.PublishAsync(new UpdateDownloadCompleted(update));

        cut.WaitForAssertion(() =>
            cut.FindComponent<IconButton>().Instance.Title.Should().Be("Restart to update to v1.2.3"));
    }

    [Fact(DisplayName = "Clicking the indicator opens the update dialog")]
    public async Task Opens_the_update_dialog()
    {
        _updates.AvailableUpdate.Returns(Update());
        var cut = Render<UpdateIndicator>();

        await cut.Find("button").ClickAsync(new MouseEventArgs());

        await _dialogs.Received(1).ShowAsync<UpdateProgressDialog>("Update Available", Arg.Any<DialogOptions>());
    }
}
