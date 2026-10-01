using FluentAssertions;
using Iris.Brokers.Amazon;
using Iris.Contracts.Brokers.Models.Amazon;
using Iris.Desktop.Brokers;
using Iris.Desktop.Infrastructure;
using Iris.Desktop.Infrastructure.ShellEnvironment;
using Microsoft.Extensions.DependencyInjection;
using Mythetech.Framework.Infrastructure.Initialization;

namespace Iris.Desktop.Test.Infrastructure;

public class AwsRegistrationTests
{
    private static ServiceProvider Built() =>
        new ServiceCollection()
            .AddLogging()
            .AddAwsProfileSupport()
            .BuildServiceProvider();

    [Fact(DisplayName = "The Amazon connection form can get its profile list")]
    public void Registers_the_profile_catalog()
    {
        using var services = Built();

        services.GetRequiredService<IAwsProfileCatalog>().Should().BeOfType<AwsProfileCatalog>();
    }

    [Fact(DisplayName = "The shell environment is imported before saved connections restore")]
    public void The_shell_hook_runs_before_restore()
    {
        using var services = Built();

        var shellHook = services.GetServices<IAsyncInitializationHook>()
            .Should().ContainSingle(hook => hook is ShellEnvironmentInitializationHook).Subject;

        // Order is all that is read from the restore hook, so it needs no real dependencies.
        var restoreHook = new RestoreConnectionsInitializationHook(null!, null!, null!, null!);

        shellHook.Order.Should().BeLessThan(restoreHook.Order);
    }
}
