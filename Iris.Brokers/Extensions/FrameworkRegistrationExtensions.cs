using System.Reflection;
using Iris.Brokers.Frameworks;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Brokers.Extensions;

public static class FrameworkRegistrationExtensions
{
    public static IServiceCollection AddFrameworkProvider(this IServiceCollection services, bool registerFrameworks = true)
    {
        services.AddSingleton<IFrameworkProvider, StaticFrameworkProvider>();
        
        if (registerFrameworks)
            services.AddSupportedFrameworks();
        
        return services;
    }

    public static IServiceCollection AddSupportedFrameworks(this IServiceCollection services)
    {
        foreach (var implementationType in Assembly.GetAssembly(typeof(IFramework))!.GetTypes()
                     .Where(t => typeof(IFramework).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract))
        {
            services.AddSingleton(typeof(IFramework), implementationType);
        }

        return services;
    }
}