using FluentAssertions;
using Iris.Components.Messaging;
using Iris.Contracts.Brokers.Models;
using Iris.Contracts.Results;
using NSubstitute;
using Xunit;

namespace Iris.Components.Test.Messaging;

public class MessageSendOrchestratorTests
{
    private readonly IMessageService _messageService = Substitute.For<IMessageService>();
    private readonly MessageState _state = new();
    private readonly MessageSendOrchestrator _sut;

    public MessageSendOrchestratorTests()
    {
        _messageService
            .SendMessageAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<Dictionary<string, string>?>())
            .Returns(new Success<bool>(true));

        _sut = new MessageSendOrchestrator(_messageService, _state);
    }

    [Fact]
    public async Task SendAsync_ExplicitFramework_OverridesAmbientState()
    {
        _state.SetFramework("MassTransit");

        await _sut.SendAsync(new SendContext
        {
            Json = "{}",
            Endpoint = new EndpointDetails { Name = "orders", Address = "a", Provider = "RabbitMq" },
            Framework = "NServiceBus",
        });

        await _messageService.Received(1).SendMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            "NServiceBus",
            Arg.Any<Dictionary<string, string>?>(),
            Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task SendAsync_NoExplicitFramework_FallsBackToAmbientState()
    {
        _state.SetFramework("MassTransit");

        await _sut.SendAsync(new SendContext
        {
            Json = "{}",
            Endpoint = new EndpointDetails { Name = "orders", Address = "a", Provider = "RabbitMq" },
        });

        await _messageService.Received(1).SendMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            "MassTransit",
            Arg.Any<Dictionary<string, string>?>(),
            Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task SendAsync_ExplicitHeaders_DoNotMutateAmbientState()
    {
        var explicitHeaders = new Dictionary<string, string> { ["panel-key"] = "panel-value" };

        await _sut.SendAsync(new SendContext
        {
            Json = "{}",
            Endpoint = new EndpointDetails { Name = "orders", Address = "a", Provider = "RabbitMq" },
            Headers = explicitHeaders,
        });

        await _messageService.Received(1).SendMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<Dictionary<string, string>?>(),
            explicitHeaders);

        _state.Headers.Should().NotContainKey("panel-key");
    }
}
