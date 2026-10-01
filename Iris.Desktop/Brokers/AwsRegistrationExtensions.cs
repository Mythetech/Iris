using Iris.Brokers.Amazon;
using Iris.Contracts.Brokers.Models.Amazon;
using Iris.Desktop.Infrastructure.ShellEnvironment;
using Microsoft.Extensions.DependencyInjection;
using Mythetech.Framework.Infrastructure.Initialization;

namespace Iris.Desktop.Brokers;

public static class AwsRegistrationExtensions
{
    /// <summary>
    /// What a desktop host adds so that Amazon connections can use the profiles on this
    /// machine: the list the connection form shows, and the startup step that gives Iris the
    /// AWS environment a terminal would have.
    /// </summary>
    public static IServiceCollection AddAwsProfileSupport(this IServiceCollection services)
    {
        services.AddSingleton<IAwsProfileCatalog, AwsProfileCatalog>();
        services.AddSingleton<LoginShellEnvironmentReader>();
        services.AddInitializationHook<ShellEnvironmentInitializationHook>();

        return services;
    }
}
