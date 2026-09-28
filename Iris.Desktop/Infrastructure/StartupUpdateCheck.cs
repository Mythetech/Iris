using Mythetech.Framework.Desktop.Updates;
using Mythetech.Framework.Infrastructure.Smoke;

namespace Iris.Desktop.Infrastructure;

/// <summary>
/// The update check the main layout runs once at launch. A smoke run launches a Velopack package versioned
/// below every real release, so the check would find an update and offer or download it mid-run; it is
/// skipped there instead.
/// </summary>
public sealed class StartupUpdateCheck(IUpdateService updates, ISmokeTestContext smokeTest)
{
    public Task RunAsync(CancellationToken cancellationToken = default) =>
        smokeTest.IsEnabled ? Task.CompletedTask : updates.CheckForUpdatesOnStartupAsync(cancellationToken);
}
