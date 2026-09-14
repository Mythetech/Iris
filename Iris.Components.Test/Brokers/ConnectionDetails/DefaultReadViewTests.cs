using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Iris.Components.Brokers;
using Iris.Components.Brokers.ConnectionDetails;
using Iris.Components.Endpoints;
using Iris.Components.Messaging;
using Iris.Contracts.Brokers.Models;
using Iris.Contracts.Messaging.Models;
using Iris.Contracts.Results;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using NSubstitute;
using Xunit;

namespace Iris.Components.Test.Brokers.ConnectionDetails;

public class DefaultReadViewTests : IrisTestContext
{
    private const string Address = "amqp://broker";

    private readonly IBrokerService _brokers = Substitute.For<IBrokerService>();
    private readonly IMessageService _messages = Substitute.For<IMessageService>();

    public DefaultReadViewTests()
    {
        _brokers.GetEndpointsAsync().Returns(
        [
            new EndpointDetails { Name = "orders", Address = Address, Provider = "rabbitmq", Type = "Queue" },
            new EndpointDetails { Name = "elsewhere", Address = "amqp://other", Provider = "rabbitmq", Type = "Queue" },
        ]);

        _messages.PeekMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
            .Returns(new Success<IReadOnlyList<ReceivedMessageDto>>([]));

        Services.AddSingleton(_brokers);
        Services.AddSingleton(_messages);
        Services.AddSingleton(new BrokerOperationSettings());

        AddPopoverProvider();
    }

    private static Provider AnyProvider() =>
        new() { Id = Guid.NewGuid(), Name = "rabbitmq", Address = Address };

    private static ReaderCapabilitiesDto Caps(
        bool peek = true, bool receive = true, bool peekDlq = false, bool receiveDlq = false) =>
        new(peek, receive, peekDlq, receiveDlq, MaxPeekBatchSize: 10, MaxReceiveBatchSize: 10);

    private IRenderedComponent<DefaultReadView> Render(ReaderCapabilitiesDto caps) =>
        Render<DefaultReadView>(p => p
            .Add(x => x.Provider, AnyProvider())
            .Add(x => x.Capabilities, caps));

    private static async Task SelectTheEndpointAsync(IRenderedComponent<DefaultReadView> cut)
    {
        var selector = cut.FindComponent<EndpointSelector>();
        await cut.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync(
            selector.Instance.Endpoints.Single(e => e.Name == "orders")));
    }

    [Fact(DisplayName = "Offers only the endpoints belonging to this connection")]
    public void Scopes_the_endpoint_list_to_the_connection()
    {
        // The view is handed a Provider and nothing else, which is why its Peek and
        // Receive buttons shipped with no handler: there was no endpoint to act on.
        var cut = Render(Caps());

        var selector = cut.FindComponent<EndpointSelector>();
        selector.Instance.Endpoints.Should().ContainSingle(e => e.Name == "orders")
            .And.NotContain(e => e.Name == "elsewhere");
    }

    [Fact(DisplayName = "Asks for an endpoint before offering any read control")]
    public void Shows_no_controls_until_an_endpoint_is_picked()
    {
        var cut = Render(Caps());

        cut.Markup.Should().Contain("Pick an endpoint");
        cut.FindComponents<MessageReaderPanel>().Should().BeEmpty();
    }

    [Fact(DisplayName = "Peek button hidden when CanPeek is false")]
    public async Task Hides_peek_when_unsupported()
    {
        var cut = Render(Caps(peek: false));
        await SelectTheEndpointAsync(cut);

        Buttons(cut).Should().NotContain("Peek").And.Contain("Receive");
    }

    [Fact(DisplayName = "DLQ controls hidden when no DLQ capability")]
    public async Task Hides_dlq_when_unsupported()
    {
        var cut = Render(Caps());
        await SelectTheEndpointAsync(cut);

        cut.Markup.Should().NotContain("Dead-letter queue");
    }

    [Fact(DisplayName = "Full capability shows all controls")]
    public async Task Shows_everything_when_fully_capable()
    {
        var cut = Render(Caps(peekDlq: true, receiveDlq: true));
        await SelectTheEndpointAsync(cut);

        Buttons(cut).Should().Contain("Peek").And.Contain("Receive");
        cut.Markup.Should().Contain("Dead-letter queue");
    }

    [Fact(DisplayName = "Peek reads from the endpoint the user picked")]
    public async Task Peek_actually_reads()
    {
        // The whole point of the item: these were four buttons with no OnClick and the
        // copy "Read results panel coming with the read-side wiring task" on a live route.
        var cut = Render(Caps());
        await SelectTheEndpointAsync(cut);

        await cut.FindAll("button").Single(b => b.TextContent.Trim() == "Peek").ClickAsync(new());

        await _messages.Received(1).PeekMessagesAsync(Address, "orders", Arg.Any<int>());
    }

    [Fact(DisplayName = "Renders no control without a handler behind it")]
    public async Task Has_no_dead_buttons()
    {
        var cut = Render(Caps(peekDlq: true, receiveDlq: true));
        await SelectTheEndpointAsync(cut);

        cut.Markup.Should().NotContain("coming with");

        cut.FindAll("button").Should().OnlyContain(
            b => b.HasAttribute("blazor:onclick") || b.HasAttribute("blazor:onmousedown"),
            "a rendered control with nothing behind it is a dead button");
    }

    private static IEnumerable<string> Buttons(IRenderedComponent<DefaultReadView> cut) =>
        cut.FindAll("button").Select(b => b.TextContent.Trim());
}
