using Iris.Desktop.Brokers;
using Iris.Desktop.History;
using Iris.Desktop.PackageManagement;
using Iris.Desktop.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Desktop.Infrastructure;

public static class PersistenceRegistrationExtensions
{
    /// <summary>
    /// One LiteDatabase for the process, and one instance of each repository over it.
    /// <para>
    /// The context and two of the repositories used to be transient. MessageRecorder is a
    /// transient <c>IConsumer&lt;MessageSent&gt;</c> that the bus resolves from the root
    /// provider, so every sent message built another context and opened another connection
    /// to the same file, and because the context is IDisposable and came from the root
    /// provider, the container kept each one alive until the process exited.
    /// </para>
    /// <para>
    /// Extracted from Program.cs so the lifetimes can be asserted. The regression is
    /// invisible at runtime, it just makes sending slower and leakier over a session.
    /// </para>
    /// </summary>
    public static IServiceCollection AddIrisPersistence(this IServiceCollection services)
    {
        services.AddSingleton<IrisLiteDbContext>();
        services.AddSingleton<HistoryRepository>();
        services.AddSingleton<ConnectionRepository>();
        services.AddSingleton<TemplateRepository>();
        services.AddSingleton<PackageRepository>();

        return services;
    }
}
