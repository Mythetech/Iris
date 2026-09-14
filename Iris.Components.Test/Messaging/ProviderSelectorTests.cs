using Bunit;
using FluentAssertions;
using Iris.Components.Messaging;
using Iris.Contracts.Brokers.Models;
using MudBlazor;
using Xunit;

namespace Iris.Components.Test.Messaging;

public class ProviderSelectorTests : IrisTestContext
{
    public ProviderSelectorTests()
    {
        AddPopoverProvider();
    }

    private static Provider ProviderAt(string address) =>
        new() { Id = Guid.NewGuid(), Name = "rabbitmq", Address = address, Endpoints = 1 };

    [Fact(DisplayName = "The parent's Value reaches the underlying MudSelect")]
    public void Forwards_the_value_it_is_given()
    {
        // The directive was spelled "bind-Value" without the leading @, so Razor emitted it
        // as an unknown HTML attribute on MudSelect and the parameter never arrived. Every
        // existing test drove the component through ValueChanged, which is the one direction
        // that always worked, so nothing caught it.
        var selected = ProviderAt("amqp://second");

        var cut = Render<ProviderSelector>(p => p
            .Add(x => x.Providers, [ProviderAt("amqp://first"), selected])
            .Add(x => x.Value, selected));

        cut.FindComponent<MudSelect<Provider>>().Instance.Value.Should().BeSameAs(selected);
    }

    [Fact(DisplayName = "A Value change from the parent is pushed down on re-render")]
    public async Task Follows_the_parent_on_a_later_change()
    {
        var first = ProviderAt("amqp://first");
        var second = ProviderAt("amqp://second");

        var cut = Render<ProviderSelector>(p => p
            .Add(x => x.Providers, [first, second])
            .Add(x => x.Value, first));

        await cut.InvokeAsync(() => cut.Render(p => p.Add(x => x.Value, second)));

        cut.FindComponent<MudSelect<Provider>>().Instance.Value.Should().BeSameAs(second);
    }
}
