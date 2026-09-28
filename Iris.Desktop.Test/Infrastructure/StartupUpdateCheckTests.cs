using Iris.Desktop.Infrastructure;
using Mythetech.Framework.Desktop.Updates;
using Mythetech.Framework.Infrastructure.Smoke;
using NSubstitute;

namespace Iris.Desktop.Test.Infrastructure;

public class StartupUpdateCheckTests
{
    private readonly IUpdateService _updates = Substitute.For<IUpdateService>();
    private readonly ISmokeTestContext _smokeTest = Substitute.For<ISmokeTestContext>();

    [Fact(DisplayName = "A normal launch runs the startup update check")]
    public async Task Checks_for_updates_on_a_normal_launch()
    {
        _smokeTest.IsEnabled.Returns(false);

        await new StartupUpdateCheck(_updates, _smokeTest).RunAsync(TestContext.Current.CancellationToken);

        await _updates.Received(1).CheckForUpdatesOnStartupAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A smoke run never checks for, offers or downloads an update")]
    public async Task Skips_the_update_check_in_a_smoke_run()
    {
        _smokeTest.IsEnabled.Returns(true);

        await new StartupUpdateCheck(_updates, _smokeTest).RunAsync(TestContext.Current.CancellationToken);

        await _updates.DidNotReceiveWithAnyArgs().CheckForUpdatesOnStartupAsync(Arg.Any<CancellationToken>());
        await _updates.DidNotReceiveWithAnyArgs().CheckForUpdatesAsync(Arg.Any<CancellationToken>());
        await _updates.DidNotReceiveWithAnyArgs().DownloadUpdateAsync(Arg.Any<CancellationToken>());
    }
}
