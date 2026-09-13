using FluentAssertions;
using Iris.Brokers.Frameworks;
using Iris.Contracts.Messaging.Frameworks;
using Xunit;

namespace Iris.Brokers.Test.Frameworks;

public class FrameworkDescriptorTests
{
    public static IEnumerable<object[]> Adapters() =>
    [
        [new MassTransitAdapter()],
        [new NServiceBusAdapter()],
        [new RebusAdapter()],
        [new BrighterAdapter()],
        [new WolverineAdapter()],
        [new EasyNetQAdapter()],
    ];

    [Theory]
    [MemberData(nameof(Adapters))]
    public void Descriptor_name_matches_adapter_name_and_keys_are_unique(IFramework adapter)
    {
        adapter.Descriptor.Name.Should().Be(adapter.Name);
        adapter.Descriptor.Inputs.Select(i => i.Key).Should().OnlyHaveUniqueItems();
        adapter.Descriptor.Inputs.Should().OnlyContain(i => !string.IsNullOrWhiteSpace(i.Label) && !string.IsNullOrWhiteSpace(i.Description));
    }

    [Fact]
    public void TypeName_is_declared_by_every_adapter_and_is_never_required()
    {
        foreach (var row in Adapters())
        {
            var adapter = (IFramework)row[0];
            var typeName = adapter.Descriptor.Inputs.SingleOrDefault(i => i.Key == FrameworkInputs.TypeName);

            typeName.Should().NotBeNull(adapter.Name);
            typeName!.Required.Should().BeFalse(adapter.Name);
        }
    }

    [Fact]
    public void EasyNetQ_declares_type_name_format_with_allowed_values()
    {
        var format = new EasyNetQAdapter().Descriptor.Inputs.Single(i => i.Key == EasyNetQAdapter.TypeNameFormatKey);

        format.Required.Should().BeFalse();
        format.DefaultValue.Should().Be("Default");
        format.AllowedValues.Should().BeEquivalentTo(new[] { "Default", "Legacy" });
    }

    [Fact]
    public void AssemblyName_is_required_only_for_EasyNetQ()
    {
        new EasyNetQAdapter().Descriptor.Inputs.Single(i => i.Key == FrameworkInputs.AssemblyName).Required.Should().BeTrue();
        new RebusAdapter().Descriptor.Inputs.Single(i => i.Key == FrameworkInputs.AssemblyName).Required.Should().BeFalse();
        new NServiceBusAdapter().Descriptor.Inputs.Single(i => i.Key == FrameworkInputs.AssemblyName).Required.Should().BeFalse();
        new MassTransitAdapter().Descriptor.Inputs.Should().NotContain(i => i.Key == FrameworkInputs.AssemblyName);
        new BrighterAdapter().Descriptor.Inputs.Should().NotContain(i => i.Key == FrameworkInputs.AssemblyName);
    }

    [Fact]
    public void Brighter_declares_topic_and_message_kind_with_allowed_values()
    {
        var inputs = new BrighterAdapter().Descriptor.Inputs;

        inputs.Should().Contain(i => i.Key == BrighterAdapter.TopicKey && !i.Required);
        var kind = inputs.Single(i => i.Key == BrighterAdapter.MessageKindKey);
        kind.DefaultValue.Should().Be("MT_EVENT");
        kind.AllowedValues.Should().BeEquivalentTo(new[] { "MT_EVENT", "MT_COMMAND", "MT_DOCUMENT" });
    }

    [Fact]
    public void VerifiedProviders_declarations()
    {
        var rabbitOnly = new IFramework[] { new RebusAdapter(), new EasyNetQAdapter(), new BrighterAdapter(), new WolverineAdapter() };
        foreach (var adapter in rabbitOnly)
            adapter.VerifiedProviders.Should().BeEquivalentTo(new[] { ConnectorProviders.RabbitMq }, adapter.Name);

        var everywhere = new IFramework[] { new MassTransitAdapter(), new NServiceBusAdapter() };
        foreach (var adapter in everywhere)
            adapter.VerifiedProviders.Should().BeEquivalentTo(ConnectorProviders.All, adapter.Name);
    }

    [Fact]
    public void Wolverine_declares_only_type_name()
    {
        new WolverineAdapter().Descriptor.Inputs.Select(i => i.Key).Should().Equal(FrameworkInputs.TypeName);
    }
}
