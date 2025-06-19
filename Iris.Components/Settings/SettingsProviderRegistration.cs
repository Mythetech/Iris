using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Components.Settings;

public static class SettingsProviderRegistration
{
    public static IServiceCollection AddSettingsProviders(this IServiceCollection services, Assembly host)
    {
        Assembly[] assemblies = [typeof(Section).Assembly, host];

        foreach (Assembly assembly in assemblies)
        {
            var sectionProviders = assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface) 
                .Where(t => typeof(ISettingsSectionProvider).IsAssignableFrom(t)); 

            foreach (var sectionProvider in sectionProviders)
            {
                services.AddSingleton(typeof(ISettingsSectionProvider), sectionProvider);
            }
        }

        services.AddSingleton<ISectionAggregator, SectionAggregator>();
        
        return services;
    }
}