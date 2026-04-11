using Bunit;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Components.Brokers.ConnectionDetails;
using Iris.Components.Messaging;
using Iris.Contracts.Brokers.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using Xunit;

namespace Iris.Components.Test.Brokers.ConnectionDetails;

public class ConnectionDetailsPageTests : TestContext
{
    private readonly IBrokerService _brokerService = Substitute.For<IBrokerService>();
    private readonly IMessageService _messageService = Substitute.For<IMessageService>();

    public ConnectionDetailsPageTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(_brokerService);
        Services.AddSingleton(_messageService);
        Services.AddSingleton(new EndpointsViewRegistry
        {
            { "rabbitmq", typeof(RabbitMqEndpointsView) },
            { "azureservicebus", typeof(AzureServiceBusEndpointsView) },
        });
        Services.AddSingleton(new ReadViewRegistry());
        Services.AddSingleton(new SendViewRegistry());
        JSInterop.Mode = JSRuntimeMode.Loose;
        RenderComponent<MudPopoverProvider>();
    }

    private static ReaderCapabilitiesDto Caps(
        bool peek = true, bool receive = true,
        bool peekDlq = false, bool receiveDlq = false)
        => new ReaderCapabilitiesDto(
            CanPeek: peek,
            CanReceive: receive,
            CanPeekDeadLetter: peekDlq,
            CanReceiveDeadLetter: receiveDlq,
            MaxPeekBatchSize: 0,
            MaxReceiveBatchSize: 0);

    [Fact(DisplayName = "Page shows 'not found' when the Id does not resolve")]
    public void Shows_not_found_when_provider_missing()
    {
        var id = Guid.NewGuid();
        _brokerService.GetConnectionByIdAsync(id).Returns((Provider?)null);

        var cut = RenderComponent<ConnectionDetailsPage>(p => p.Add(x => x.Id, id));

        cut.Markup.Should().Contain("Connection not found");
    }

    [Fact(DisplayName = "Page renders provider name in header when found")]
    public void Renders_provider_in_header()
    {
        var id = Guid.NewGuid();
        var provider = new Provider { Id = id, Name = "RabbitMq", Address = "amqp://x", Transport = "RabbitMq" };
        _brokerService.GetConnectionByIdAsync(id).Returns(provider);
        _brokerService.GetReaderCapabilitiesAsync(provider.Address).Returns(Caps());
        _brokerService.GetEndpointsAsync().Returns(new List<EndpointDetails>());

        var cut = RenderComponent<ConnectionDetailsPage>(p => p.Add(x => x.Id, id));

        cut.Markup.Should().Contain("RabbitMq");
        cut.Markup.Should().Contain("amqp://x");
    }

    [Fact(DisplayName = "Read tab is hidden when no read capability is supported")]
    public void Hides_read_tab_when_no_capabilities()
    {
        var id = Guid.NewGuid();
        var provider = new Provider { Id = id, Name = "RabbitMq", Address = "amqp://x", Transport = "RabbitMq" };
        _brokerService.GetConnectionByIdAsync(id).Returns(provider);
        _brokerService.GetReaderCapabilitiesAsync(provider.Address).Returns(Caps(peek: false, receive: false));
        _brokerService.GetEndpointsAsync().Returns(new List<EndpointDetails>());

        var cut = RenderComponent<ConnectionDetailsPage>(p => p.Add(x => x.Id, id));

        // Tab labels render in markup. Look for ">Read<" rather than just "Read" to avoid
        // false matches from "Reader", "Read message", etc.
        cut.Markup.Should().NotContain(">Read<");
    }

    [Fact(DisplayName = "Endpoints tab uses the registered RabbitMq view (matched via Provider.Name fallback)")]
    public void Uses_rabbitmq_view_when_provider_name_matches()
    {
        var id = Guid.NewGuid();
        // Transport mutates to "Docker" on localhost in real life — that's the case
        // where Transport-key misses and Provider.Name fallback is the only thing keeping
        // the right view selected. Reproduce that exact shape here.
        var provider = new Provider { Id = id, Name = "RabbitMq", Address = "amqp://x", Transport = "Docker" };
        _brokerService.GetConnectionByIdAsync(id).Returns(provider);
        _brokerService.GetReaderCapabilitiesAsync(provider.Address).Returns(Caps());
        _brokerService.GetEndpointsAsync().Returns(new List<EndpointDetails>
        {
            new() { Name = "orders", Address = "amqp://x", Provider = "RabbitMq", Type = "Queue" },
        });

        var cut = RenderComponent<ConnectionDetailsPage>(p => p.Add(x => x.Id, id));

        cut.FindComponent<RabbitMqEndpointsView>().Should().NotBeNull();
    }

    [Fact(DisplayName = "Endpoints tab uses the registered Azure Service Bus view (matched via Provider.Transport)")]
    public void Uses_azure_service_bus_view_when_transport_matches()
    {
        var id = Guid.NewGuid();
        // Both Azure adapters share Provider.Name = "Azure" — Transport is the only
        // thing distinguishing SB from Queue Storage.
        var provider = new Provider { Id = id, Name = "Azure", Address = "sb://x", Transport = "AzureServiceBus" };
        _brokerService.GetConnectionByIdAsync(id).Returns(provider);
        _brokerService.GetReaderCapabilitiesAsync(provider.Address).Returns(Caps());
        _brokerService.GetEndpointsAsync().Returns(new List<EndpointDetails>());

        var cut = RenderComponent<ConnectionDetailsPage>(p => p.Add(x => x.Id, id));

        cut.FindComponent<AzureServiceBusEndpointsView>().Should().NotBeNull();
    }

    [Fact(DisplayName = "Endpoints tab falls back to DefaultEndpointsView for unregistered brokers")]
    public void Falls_back_to_default_view_when_unregistered()
    {
        var id = Guid.NewGuid();
        var provider = new Provider { Id = id, Name = "ExoticBroker", Address = "exotic://x", Transport = "ExoticTransport" };
        _brokerService.GetConnectionByIdAsync(id).Returns(provider);
        _brokerService.GetReaderCapabilitiesAsync(provider.Address).Returns(Caps());
        _brokerService.GetEndpointsAsync().Returns(new List<EndpointDetails>());

        var cut = RenderComponent<ConnectionDetailsPage>(p => p.Add(x => x.Id, id));

        cut.FindComponent<DefaultEndpointsView>().Should().NotBeNull();
    }
}
