using Microsoft.Extensions.DependencyInjection;

namespace Iris.Components.CommandPalette;

public static class CommandPaletteServiceCollectionExtensions
{
    /// <summary>
    /// Registers the command palette service and its built-in command providers.
    /// Future palette features (e.g. switch-connection, jump-to-history) plug in by
    /// registering additional <see cref="ICommandProvider"/> implementations against
    /// the same DI container — no changes to this method required.
    /// </summary>
    public static IServiceCollection AddCommandPalette(this IServiceCollection services)
    {
        services.AddScoped<CommandPaletteService>();
        services.AddScoped<ICommandProvider, NavigationPaletteCommandProvider>();
        return services;
    }
}
