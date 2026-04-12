using Bunit;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Components.Endpoints;
using Iris.Contracts.Brokers.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using Xunit;

namespace Iris.Components.Test.Endpoints;

public class EndpointOffcanvasTests : TestContext
{
    private readonly IBrokerService _brokerService = Substitute.For<IBrokerService>();

    public EndpointOffcanvasTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(_brokerService);
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    private static EndpointDetails Sample(string name = "orders") => new()
    {
        Name = name,
        Address = "amqp://x",
        Provider = "RabbitMq",
        Type = "Queue",
    };

    [Fact(DisplayName = "Offcanvas renders endpoint name through MudText, not raw <h6>")]
    public void Header_uses_mudtext_not_raw_h6()
    {
        _brokerService.GetEndpointPropertiesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((EndpointPropertiesDto?)null);

        var cut = RenderComponent<EndpointOffcanvas>(p => p
            .Add(x => x.Endpoint, Sample()));

        // The bare <h6>orders</h6> bug — markup must NOT contain it.
        cut.Markup.Should().NotContain("<h6>orders</h6>");
        // MudText with Typo.h5 renders as h5 by default; the endpoint-name class
        // is the durable hook regardless of MudBlazor's chosen tag.
        cut.Markup.Should().Contain("endpoint-name");
        cut.Markup.Should().Contain("orders");
    }

    [Fact(DisplayName = "Read button is disabled with reason when CanRead is false")]
    public void Read_button_disabled_when_cannot_read()
    {
        _brokerService.GetEndpointPropertiesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((EndpointPropertiesDto?)null);

        var cut = RenderComponent<EndpointOffcanvas>(p => p
            .Add(x => x.Endpoint, Sample())
            .Add(x => x.CanRead, false)
            .Add(x => x.ReadDisabledReason, "Broker does not support reading messages"));

        var readButton = cut.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Read"));
        readButton.Should().NotBeNull();
        readButton!.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact(DisplayName = "Read button click invokes OnReadClicked callback")]
    public void Read_button_invokes_callback()
    {
        _brokerService.GetEndpointPropertiesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((EndpointPropertiesDto?)null);

        EndpointDetails? clicked = null;
        var cut = RenderComponent<EndpointOffcanvas>(p => p
            .Add(x => x.Endpoint, Sample())
            .Add(x => x.CanRead, true)
            .Add(x => x.OnReadClicked, ep => clicked = ep));

        var readButton = cut.FindAll("button").First(b => b.TextContent.Contains("Read"));
        readButton.Click();

        clicked.Should().NotBeNull();
        clicked!.Name.Should().Be("orders");
    }

    [Fact(DisplayName = "Properties section shows 'not available' when service returns null")]
    public void Shows_unavailable_when_service_returns_null()
    {
        _brokerService.GetEndpointPropertiesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((EndpointPropertiesDto?)null);

        var cut = RenderComponent<EndpointOffcanvas>(p => p
            .Add(x => x.Endpoint, Sample()));

        cut.Markup.Should().Contain("Properties are not available for this broker.");
    }

    [Fact(DisplayName = "Properties section renders broker key/value entries")]
    public void Renders_property_entries()
    {
        var dto = new EndpointPropertiesDto(new List<EndpointPropertyEntry>
        {
            new("Messages", "42"),
            new("Durable", "True"),
            new("Bindings", "amq.topic → orders.*"),
        });
        _brokerService.GetEndpointPropertiesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        var cut = RenderComponent<EndpointOffcanvas>(p => p
            .Add(x => x.Endpoint, Sample()));

        cut.Markup.Should().Contain("Messages");
        cut.Markup.Should().Contain("42");
        cut.Markup.Should().Contain("Durable");
        cut.Markup.Should().Contain("Bindings");
        cut.Markup.Should().Contain("amq.topic");
    }

    [Fact(DisplayName = "Refresh button forces re-fetch even when cached")]
    public void Refresh_bypasses_cache()
    {
        var first = new EndpointPropertiesDto(new List<EndpointPropertyEntry> { new("Messages", "1") });
        var second = new EndpointPropertiesDto(new List<EndpointPropertyEntry> { new("Messages", "5678") });
        _brokerService.GetEndpointPropertiesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(first, second);

        var cut = RenderComponent<EndpointOffcanvas>(p => p
            .Add(x => x.Endpoint, Sample()));

        var valueElements = cut.FindAll(".key-value-row-value");
        valueElements.Should().Contain(e => e.TextContent.Contains("1"));

        var refreshButton = cut.FindAll("button")
            .First(b => b.OuterHtml.Contains("M17.65 6.35"));
        refreshButton.Click();

        cut.WaitForState(() => cut.FindAll(".key-value-row-value").Any(e => e.TextContent.Contains("5678")));
        cut.FindAll(".key-value-row-value").Should().Contain(e => e.TextContent.Contains("5678"));
    }
}
