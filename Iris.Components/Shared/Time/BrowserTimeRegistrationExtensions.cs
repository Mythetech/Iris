using Microsoft.Extensions.DependencyInjection;

namespace Iris.Components.Shared.Time;

public static class BrowserTimeRegistrationExtensions
{
    public static IServiceCollection AddBrowserTimeProvider(this IServiceCollection services)
        => services.AddScoped<TimeProvider, BrowserTimeProvider>();
}