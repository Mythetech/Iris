using FluentAssertions;
using Iris.Brokers;
using Iris.Brokers.Frameworks;
using Iris.Desktop.Brokers;
using NSubstitute;
using ConnectorProviders = Iris.Contracts.Brokers.Models.ConnectorProviders;
using ConnectorTransports = Iris.Contracts.Brokers.Models.ConnectorTransports;

namespace Iris.Desktop.Test.Brokers;

/// <summary>
/// Which adapter the catalog reports as verified against which connection.
///
/// <para>
/// The answer is user-facing copy: an unverified pairing carries a warning in the framework
/// picker. Getting it wrong in either direction is a defect that no exception marks, which
/// is how a claim covering both Azure transports survived until an actual round-trip
/// existed for one of them.
/// </para>
/// </summary>
public class FrameworkCatalogTests
{
    private const string Address = "broker://test";

    private static async Task<bool> IsVerified(IFramework framework, RecordingConnection connection)
    {
        var connections = Substitute.For<IBrokerConnectionManager>();
        connections.GetConnectionAsync(Address).Returns(Task.FromResult<IConnection?>(connection));

        var catalog = new LocalFrameworkCatalog([framework], connections);
        var descriptors = await catalog.GetFrameworksAsync(Address, userHeaderCount: 0);

        return descriptors.Single().Verified;
    }

    private static RecordingConnection Connection(string provider, string transport) =>
        new(provider) { Name = transport, Address = Address };

    [Fact(DisplayName = "NServiceBus is verified on Azure Queue Storage, the transport it was proven against")]
    public async Task NServiceBus_is_verified_on_queue_storage()
    {
        var verified = await IsVerified(
            new NServiceBusAdapter(),
            Connection(ConnectorProviders.Azure, ConnectorTransports.AzureQueueStorage));

        verified.Should().BeTrue();
    }

    [Fact(DisplayName = "NServiceBus is not verified on Azure Service Bus, which shares the same provider")]
    public async Task NServiceBus_is_not_verified_on_service_bus()
    {
        var verified = await IsVerified(
            new NServiceBusAdapter(),
            Connection(ConnectorProviders.Azure, ConnectorTransports.AzureServiceBus));

        // Both connections report provider "Azure". Matching on that alone, which is what the
        // catalog used to do, cannot tell these two cases apart, and one of them is wrong.
        verified.Should().BeFalse();
    }

    [Fact(DisplayName = "MassTransit is verified on Azure Service Bus, where its round trip runs")]
    public async Task MassTransit_is_verified_on_service_bus()
    {
        var verified = await IsVerified(
            new MassTransitAdapter(),
            Connection(ConnectorProviders.Azure, ConnectorTransports.AzureServiceBus));

        verified.Should().BeTrue();
    }

    [Fact(DisplayName = "MassTransit is not verified on Azure Queue Storage, which it has never been run against")]
    public async Task MassTransit_is_not_verified_on_queue_storage()
    {
        var verified = await IsVerified(
            new MassTransitAdapter(),
            Connection(ConnectorProviders.Azure, ConnectorTransports.AzureQueueStorage));

        // The overclaim in the other direction, and the one that was live: ConnectorProviders.All
        // told every Queue Storage user that MassTransit was proven there.
        verified.Should().BeFalse();
    }

    [Theory(DisplayName = "A RabbitMQ adapter stays verified whatever the connection renamed itself to")]
    [InlineData(ConnectorTransports.RabbitMq)]
    [InlineData("Docker")]
    [InlineData("CloudAmpq")]
    public async Task Rabbit_adapters_survive_the_deployment_label(string transport)
    {
        // RabbitMqConnection overwrites its own name by address, so the transport is not a
        // stable key here. It has one transport, so the provider is, and the fallback is what
        // keeps a local Docker broker from reading as unverified.
        var verified = await IsVerified(
            new RebusAdapter(),
            Connection(ConnectorProviders.RabbitMq, transport));

        verified.Should().BeTrue();
    }

    [Fact(DisplayName = "An adapter proven elsewhere is unverified on a broker it has never seen")]
    public async Task An_unrelated_broker_is_unverified()
    {
        var verified = await IsVerified(
            new RebusAdapter(),
            Connection(ConnectorProviders.Amazon, ConnectorTransports.SimpleQueueService));

        verified.Should().BeFalse();
    }

    [Fact(DisplayName = "Every claim names a real provider or transport, not a typo")]
    public void Claims_are_known_identifiers()
    {
        // Claims are strings matched at runtime. A misspelled one never matches anything and
        // shows up only as an adapter that is quietly unverified everywhere, which is exactly
        // how it would be missed.
        var known = Constants(typeof(ConnectorProviders)).Concat(Constants(typeof(ConnectorTransports)));

        IFramework[] adapters =
        [
            new MassTransitAdapter(), new NServiceBusAdapter(), new RebusAdapter(),
            new BrighterAdapter(), new WolverineAdapter(), new EasyNetQAdapter(),
        ];

        foreach (var adapter in adapters)
            adapter.VerifiedProviders.Should().BeSubsetOf(known, adapter.Name);
    }

    private static IEnumerable<string> Constants(Type type) =>
        type.GetFields().Where(f => f.FieldType == typeof(string)).Select(f => (string)f.GetValue(null)!);
}
