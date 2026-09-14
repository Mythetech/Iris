using FluentAssertions;
using Iris.Desktop.Brokers;
using Iris.Desktop.History;
using Iris.Desktop.Infrastructure;
using Iris.Desktop.PackageManagement;
using Iris.Desktop.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Desktop.Test.Infrastructure;

/// <summary>
/// Asserted on the registrations rather than by resolving them, because resolving
/// IrisLiteDbContext opens the real database under the user's application data folder,
/// which a test has no business touching. The defect was the lifetime itself, so the
/// lifetime is the right thing to pin.
/// </summary>
public class PersistenceRegistrationTests
{
    private static IServiceCollection Registered() =>
        new ServiceCollection().AddIrisPersistence();

    [Theory(DisplayName = "Persistence types are registered exactly once, as singletons")]
    [InlineData(typeof(IrisLiteDbContext))]
    [InlineData(typeof(HistoryRepository))]
    [InlineData(typeof(ConnectionRepository))]
    [InlineData(typeof(TemplateRepository))]
    [InlineData(typeof(PackageRepository))]
    public void Registers_one_of_each(Type type)
    {
        // The context, HistoryRepository and ConnectionRepository were transient. Because
        // MessageRecorder is a transient consumer resolved from the root provider, every
        // sent message opened another LiteDatabase on the same file and the root container
        // held it, undisposed, for the life of the process.
        Registered()
            .Should().ContainSingle(d => d.ServiceType == type)
            .Which.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact(DisplayName = "Every repository is built over the same database context")]
    public void Shares_one_context()
    {
        Registered()
            .Where(d => typeof(IRepository).IsAssignableFrom(d.ServiceType))
            .Should().OnlyContain(d => d.Lifetime == ServiceLifetime.Singleton)
            .And.NotBeEmpty();
    }
}
