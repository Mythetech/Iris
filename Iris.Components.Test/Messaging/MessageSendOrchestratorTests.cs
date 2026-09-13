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

    [Fact]
    public async Task SendAsync_ExplicitRepeat_OverridesAmbientState()
    {
        _state.SetRepeat(5);

        await _sut.SendAsync(new SendContext
        {
            Json = "{}",
            Endpoint = new EndpointDetails { Name = "orders", Address = "a", Provider = "RabbitMq" },
            Repeat = 0,
        });

        // Repeat=0 means a single send. If the explicit value were ignored in favour
        // of the ambient Repeat=5, this would be six sends instead of one.
        await _messageService.Received(1).SendMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<Dictionary<string, string>?>(),
            Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task SendAsync_NoExplicitRepeat_FallsBackToAmbientState()
    {
        _state.SetRepeat(2);

        await _sut.SendAsync(new SendContext
        {
            Json = "{}",
            Endpoint = new EndpointDetails { Name = "orders", Address = "a", Provider = "RabbitMq" },
        });

        // Ambient Repeat=2 means repeat + 1 = 3 total sends, matching pre-existing behaviour.
        await _messageService.Received(3).SendMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<Dictionary<string, string>?>(),
            Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task SendAsync_ExplicitDelay_OverridesAmbientState()
    {
        _state.SetDelay(30);

        var elapsed = System.Diagnostics.Stopwatch.StartNew();

        await _sut.SendAsync(new SendContext
        {
            Json = "{}",
            Endpoint = new EndpointDetails { Name = "orders", Address = "a", Provider = "RabbitMq" },
            Delay = 0,
        });

        elapsed.Stop();

        // Delay=0 must not wait at all. If the ambient Delay=30 seconds were used
        // instead, this send would spend real wall-clock time in HandleDelayAsync.
        elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task SendAsync_IsolateFromMessageState_DoesNotWriteEndpointMetadataToAmbientState()
    {
        await _sut.SendAsync(new SendContext
        {
            Json = "{}",
            Endpoint = new EndpointDetails { Name = "orders", Address = "a", Provider = "RabbitMq", Type = "Queue" },
            IsolateFromMessageState = true,
        });

        _state.AdditionalProperties.Should().NotContain(x => x.Key == "EndpointType");
    }

    [Fact]
    public async Task SendAsync_NotIsolated_WritesEndpointMetadataToAmbientState_MatchingExistingBehaviour()
    {
        await _sut.SendAsync(new SendContext
        {
            Json = "{}",
            Endpoint = new EndpointDetails { Name = "orders", Address = "a", Provider = "RabbitMq", Type = "Queue" },
        });

        _state.AdditionalProperties.Should().Contain(x => x.Key == "EndpointType" && x.Value == "Queue");
    }
}
