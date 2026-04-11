using Bunit;
using FluentAssertions;
using Iris.Components.Brokers.ConnectionDetails;
using Iris.Components.Messaging;
using Iris.Contracts.Brokers.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;
using Xunit;

namespace Iris.Components.Test.Brokers.ConnectionDetails;

public class DefaultReadViewTests : TestContext
{
    public DefaultReadViewTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(Substitute.For<IMessageService>());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static Provider AnyProvider() =>
        new() { Id = Guid.NewGuid(), Name = "Sample", Address = "amqp://x" };

    [Fact(DisplayName = "Peek button hidden when CanPeek is false")]
    public void Hides_peek_when_unsupported()
    {
        var caps = new ReaderCapabilitiesDto(
            CanPeek: false,
            CanReceive: true,
            CanPeekDeadLetter: false,
            CanReceiveDeadLetter: false,
            MaxPeekBatchSize: 0,
            MaxReceiveBatchSize: 10);

        var cut = RenderComponent<DefaultReadView>(p => p
            .Add(x => x.Provider, AnyProvider())
            .Add(x => x.Capabilities, caps));

        cut.Markup.Should().NotContain("Peek");
        cut.Markup.Should().Contain("Receive");
    }

    [Fact(DisplayName = "DLQ controls hidden when no DLQ capability")]
    public void Hides_dlq_when_unsupported()
    {
        var caps = new ReaderCapabilitiesDto(
            CanPeek: true,
            CanReceive: true,
            CanPeekDeadLetter: false,
            CanReceiveDeadLetter: false,
            MaxPeekBatchSize: 10,
            MaxReceiveBatchSize: 10);

        var cut = RenderComponent<DefaultReadView>(p => p
            .Add(x => x.Provider, AnyProvider())
            .Add(x => x.Capabilities, caps));

        cut.Markup.Should().NotContain("Dead Letter");
    }

    [Fact(DisplayName = "Full capability shows all controls")]
    public void Shows_everything_when_fully_capable()
    {
        var caps = new ReaderCapabilitiesDto(
            CanPeek: true,
            CanReceive: true,
            CanPeekDeadLetter: true,
            CanReceiveDeadLetter: true,
            MaxPeekBatchSize: 10,
            MaxReceiveBatchSize: 10);

        var cut = RenderComponent<DefaultReadView>(p => p
            .Add(x => x.Provider, AnyProvider())
            .Add(x => x.Capabilities, caps));

        cut.Markup.Should().Contain("Peek");
        cut.Markup.Should().Contain("Receive");
        cut.Markup.Should().Contain("Dead Letter");
    }
}
