using FluentAssertions;
using Iris.Components.Brokers.ConnectionDetails;
using Iris.Components.Infrastructure;
using Xunit;

namespace Iris.Components.Test.Infrastructure;

public class ComponentRegistryTests
{
    private static ComponentRegistry<IConnectionEndpointsView> Registry()
        => new ComponentRegistry<IConnectionEndpointsView>()
            .Register<RabbitMqEndpointsView>("RabbitMq")
            .Register<AzureServiceBusEndpointsView>("AzureServiceBus");

    [Fact(DisplayName = "Resolves the component registered for a key")]
    public void Resolves_registered_key()
    {
        Registry().Resolve(typeof(DefaultEndpointsView), "RabbitMq")
            .Should().Be<RabbitMqEndpointsView>();
    }

    [Theory(DisplayName = "Normalizes spacing, punctuation, and casing into the same key")]
    [InlineData("AzureServiceBus")]
    [InlineData("Azure Service Bus")]
    [InlineData("azureservicebus")]
    [InlineData("AZURE_SERVICE_BUS")]
    [InlineData("azure-service.bus")]
    public void Normalizes_key_spellings(string key)
    {
        Registry().Resolve(typeof(DefaultEndpointsView), key)
            .Should().Be<AzureServiceBusEndpointsView>();
    }

    [Fact(DisplayName = "Earlier candidate keys win over later ones")]
    public void First_matching_candidate_wins()
    {
        // Transport is passed first precisely so a broker that distinguishes its
        // transports beats the coarser provider name.
        Registry().Resolve(typeof(DefaultEndpointsView), "AzureServiceBus", "RabbitMq")
            .Should().Be<AzureServiceBusEndpointsView>();
    }

    [Fact(DisplayName = "Falls through to a later candidate when the first is unregistered")]
    public void Falls_through_to_later_candidate()
    {
        // The real RabbitMq shape: Transport mutates to "Docker", so only the
        // provider name still matches.
        Registry().Resolve(typeof(DefaultEndpointsView), "Docker", "RabbitMq")
            .Should().Be<RabbitMqEndpointsView>();
    }

    [Fact(DisplayName = "Returns the fallback when no candidate is registered")]
    public void Returns_fallback_when_unregistered()
    {
        Registry().Resolve(typeof(DefaultEndpointsView), "ExoticTransport", "ExoticBroker")
            .Should().Be<DefaultEndpointsView>();
    }

    [Theory(DisplayName = "Ignores null, empty, and punctuation-only candidate keys")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-.-")]
    public void Ignores_blank_candidates(string? key)
    {
        Registry().TryResolve(out _, key).Should().BeFalse();
    }

    [Fact(DisplayName = "Skips a blank candidate and still matches a later one")]
    public void Skips_blank_candidate_before_a_match()
    {
        Registry().Resolve(typeof(DefaultEndpointsView), null, "RabbitMq")
            .Should().Be<RabbitMqEndpointsView>();
    }

    [Fact(DisplayName = "An empty registry always yields the fallback")]
    public void Empty_registry_yields_fallback()
    {
        new ComponentRegistry<IConnectionReadView>()
            .Resolve(typeof(DefaultReadView), "RabbitMq")
            .Should().Be<DefaultReadView>();
    }

    [Fact(DisplayName = "Re-registering a key replaces the previous component")]
    public void Re_registering_replaces()
    {
        new ComponentRegistry<IConnectionEndpointsView>()
            .Register<RabbitMqEndpointsView>("RabbitMq")
            .Register<AzureServiceBusEndpointsView>("RabbitMq")
            .Resolve(typeof(DefaultEndpointsView), "RabbitMq")
            .Should().Be<AzureServiceBusEndpointsView>();
    }

    [Theory(DisplayName = "Rejects a registration key with no alphanumeric content")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-.-")]
    public void Rejects_blank_registration_key(string key)
    {
        var register = () => new ComponentRegistry<IConnectionEndpointsView>()
            .Register<RabbitMqEndpointsView>(key);

        register.Should().Throw<ArgumentException>().WithParameterName("key");
    }
}
