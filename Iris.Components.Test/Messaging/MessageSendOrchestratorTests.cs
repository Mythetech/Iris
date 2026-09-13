using FluentAssertions;
using Iris.Components.Messaging;
using Iris.Contracts.Brokers.Models;
using Iris.Contracts.Messaging.Frameworks;
using Iris.Contracts.Results;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Xunit;

namespace Iris.Components.Test.Messaging;

public class MessageSendOrchestratorTests
{
    private readonly IMessageService _messageService = Substitute.For<IMessageService>();
    private readonly MessageState _state = new(new MessagingSettings(), Substitute.For<IMessageBus>());
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
        _state.SetFramework(new FrameworkDescriptor("MassTransit", []));

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
        _state.SetFramework(new FrameworkDescriptor("MassTransit", []));

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

    [Fact]
    public async Task SendAsync_SendsTheHeaderGrid_NotAnEmptyDictionary()
    {
        _state.AddHeader(new DictionaryViewModel { Key = "tenant", Value = "acme" });

        await _sut.SendAsync(new SendContext
        {
            Json = "{}",
            Endpoint = new EndpointDetails { Name = "orders", Address = "a", Provider = "RabbitMq" },
        });

        await _messageService.Received(1).SendMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<Dictionary<string, string>?>(),
            Arg.Is<Dictionary<string, string>>(h => h["tenant"] == "acme"));
    }

    [Fact]
    public async Task SendAsync_ExplicitHeaders_LeaveTheHeaderGridAlone()
    {
        var before = _state.HeaderMap.Count;

        await _sut.SendAsync(new SendContext
        {
            Json = "{}",
            Endpoint = new EndpointDetails { Name = "orders", Address = "a", Provider = "RabbitMq" },
            Headers = new Dictionary<string, string> { ["panel-key"] = "panel-value" },
        });

        _state.HeaderMap.Count.Should().Be(before);
        _state.HeaderMap.Should().NotContain(h => h.Key == "panel-key");
    }

    [Fact]
    public async Task SendAsync_TypeNameInput_NeverChangesTheQueue()
    {
        _state.SetFramework(new FrameworkDescriptor("Rebus", [new FrameworkInput(FrameworkInputs.TypeName, "Type name", "d")]));
        _state.AdditionalProperties.Single(r => r.Key == FrameworkInputs.TypeName).Value = "MyApp.Messages.OrderPlaced";

        await _sut.SendAsync(new SendContext
        {
            Json = "{}",
            Endpoint = new EndpointDetails { Name = "orders", Address = "a", Provider = "RabbitMq" },
        });

        await _messageService.Received(1).SendMessageAsync(
            "orders", Arg.Any<string>(), Arg.Any<string?>(), "Rebus",
            Arg.Is<Dictionary<string, string>>(p => p[FrameworkInputs.TypeName] == "MyApp.Messages.OrderPlaced"),
            Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task SendAsync_MessageTypeOverride_BecomesTheTypeNameInput_NotTheQueue()
    {
        await _sut.SendAsync(new SendContext
        {
            Json = "{}",
            Endpoint = new EndpointDetails { Name = "orders", Address = "a", Provider = "RabbitMq" },
            MessageTypeOverride = "MyApp.Messages.OrderPlaced",
        });

        await _messageService.Received(1).SendMessageAsync(
            "orders", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Is<Dictionary<string, string>>(p => p[FrameworkInputs.TypeName] == "MyApp.Messages.OrderPlaced"),
            Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task SendAsync_Isolated_DoesNotSendTheGridProperties()
    {
        _state.SetFramework(new FrameworkDescriptor("Rebus", [new FrameworkInput(FrameworkInputs.TypeName, "Type name", "d")]));
        _state.AdditionalProperties.Single(r => r.Key == FrameworkInputs.TypeName).Value = "MyApp.Messages.OrderPlaced";

        await _sut.SendAsync(new SendContext
        {
            Json = "{}",
            Endpoint = new EndpointDetails { Name = "orders", Address = "a", Provider = "RabbitMq" },
            IsolateFromMessageState = true,
        });

        await _messageService.Received(1).SendMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Is<Dictionary<string, string>>(p => !p.ContainsKey(FrameworkInputs.TypeName)),
            Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task SendAsync_MissingRequiredInput_ForAContextFramework_Fails()
    {
        _state.SetAvailableFrameworks(
        [
            new FrameworkDescriptor("EasyNetQ",
            [
                new FrameworkInput(FrameworkInputs.AssemblyName, "Assembly name", "d", Required: true),
            ]),
        ]);

        var result = await _sut.SendAsync(new SendContext
        {
            Json = "{}",
            Endpoint = new EndpointDetails { Name = "orders", Address = "a", Provider = "RabbitMq" },
            Framework = "EasyNetQ",
        });

        result.Error.Should().BeTrue();
        result.Message.Should().Be("Assembly name is required for EasyNetQ.");
        await _messageService.DidNotReceiveWithAnyArgs().SendMessageAsync(default!, default!, default, default, default, default);
    }

    [Fact]
    public async Task SendAsync_Isolated_SkipsRequiredInputValidation()
    {
        _state.SetAvailableFrameworks(
        [
            new FrameworkDescriptor("EasyNetQ",
            [
                new FrameworkInput(FrameworkInputs.AssemblyName, "Assembly name", "d", Required: true),
            ]),
        ]);

        var result = await _sut.SendAsync(new SendContext
        {
            Json = "{}",
            Endpoint = new EndpointDetails { Name = "orders", Address = "a", Provider = "RabbitMq" },
            Framework = "EasyNetQ",
            IsolateFromMessageState = true,
        });

        result.Error.Should().BeFalse();
        await _messageService.Received(1).SendMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            "EasyNetQ",
            Arg.Any<Dictionary<string, string>?>(),
            Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task SendAsync_MissingRequiredInput_FailsBeforeSending()
    {
        _state.SetFramework(new FrameworkDescriptor("EasyNetQ",
        [
            new FrameworkInput(FrameworkInputs.AssemblyName, "Assembly name", "d", Required: true),
        ]));

        var result = await _sut.SendAsync(new SendContext
        {
            Json = "{}",
            Endpoint = new EndpointDetails { Name = "orders", Address = "a", Provider = "RabbitMq" },
        });

        result.Error.Should().BeTrue();
        result.Message.Should().Be("Assembly name is required for EasyNetQ.");
        await _messageService.DidNotReceiveWithAnyArgs().SendMessageAsync(default!, default!, default, default, default, default);
    }

    [Fact]
    public async Task SendAsync_RequiredInputCheck_OnlyAppliesToTheSelectedFramework()
    {
        _state.SetFramework(new FrameworkDescriptor("EasyNetQ",
        [
            new FrameworkInput(FrameworkInputs.AssemblyName, "Assembly name", "d", Required: true),
        ]));

        var result = await _sut.SendAsync(new SendContext
        {
            Json = "{}",
            Endpoint = new EndpointDetails { Name = "orders", Address = "a", Provider = "RabbitMq" },
            Framework = "MassTransit",
        });

        result.Error.Should().BeFalse();
    }
}
