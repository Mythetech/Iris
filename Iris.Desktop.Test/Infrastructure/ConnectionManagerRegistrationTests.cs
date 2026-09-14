using FluentAssertions;
using Iris.Components;
using Iris.Components.Brokers;
using Iris.Components.Messaging;
using Iris.Desktop.Admin;
using Iris.Desktop.Brokers;
using Iris.Desktop.History;
using Iris.Desktop.PackageManagement;
using Iris.Desktop.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Desktop.Test.Infrastructure;

public class ConnectionManagerRegistrationTests
{
    /// <summary>
    /// The real combination the desktop host passes, so the assertions below describe what
    /// Iris actually builds rather than an arrangement invented for the test.
    /// </summary>
    private static IServiceCollection Registered() =>
        new ServiceCollection()
            .AddIrisComponentServices<
                LocalConnectionManager,
                LocalConnectionManager,
                LocalTemplateService,
                LocalPackageService,
                LocalHistoryService,
                AdminClient,
                MessageLayoutRepository>();

    [Fact(DisplayName = "One LocalConnectionManager serves both the broker and message roles")]
    public void Registers_the_connection_manager_once()
    {
        // IBrokerService and IMessageService used to be two separate AddScoped calls onto
        // the same class, so each scope built it twice and every copy took its own
        // ConnectionRepository and, through that, its own database handle.
        Registered()
            .Should().ContainSingle(d => d.ServiceType == typeof(LocalConnectionManager))
            .Which.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Theory(DisplayName = "Both interfaces resolve through the concrete registration")]
    [InlineData(typeof(IBrokerService))]
    [InlineData(typeof(IMessageService))]
    public void Maps_the_interfaces_onto_it(Type serviceType)
    {
        var descriptor = Registered().Should().ContainSingle(d => d.ServiceType == serviceType).Subject;

        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
        descriptor.ImplementationFactory.Should().NotBeNull(
            "a direct implementation type would give each interface its own instance again");
        descriptor.ImplementationType.Should().BeNull();
    }
}
