using FluentAssertions;
using Iris.Components.Messaging;
using Mythetech.Framework.Infrastructure.MessageBus;
using Mythetech.Framework.Infrastructure.Settings.Events;
using NSubstitute;

namespace Iris.Components.Test.Messaging;

public class MessageStateIrisHeaderTests
{
    private const string IrisKey = "iris-key";

    private readonly MessagingSettings _settings = new();
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    private MessageState CreateState() => new(_settings, _bus);

    private IConsumer<SettingsModelChanged<MessagingSettings>> SubscriptionFor(MessageState state)
        => (IConsumer<SettingsModelChanged<MessagingSettings>>)_bus
            .ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IMessageBus.Subscribe))
            .GetArguments()[0]!;

    [Fact(DisplayName = "The iris key setting is on by default")]
    public void Setting_Defaults_On()
    {
        new MessagingSettings().SendIrisHeader.Should().BeTrue();
        CreateState().SendIrisHeader.Should().BeTrue();
    }

    [Fact(DisplayName = "Turning the setting off stops the iris key being sent")]
    public void Setting_Off_Stops_The_Send()
    {
        var state = CreateState();

        _settings.SendIrisHeader = false;

        state.SendIrisHeader.Should().BeFalse();
    }

    [Fact(DisplayName = "A state built while the setting is off never seeds the iris key header")]
    public void Setting_Off_At_Startup_Seeds_No_Header()
    {
        _settings.SendIrisHeader = false;

        var state = CreateState();

        state.SendIrisHeader.Should().BeFalse();
        state.GetHeaders().Should().NotContainKey(IrisKey);
    }

    [Fact(DisplayName = "A settings change drops the iris key row and notifies subscribers")]
    public async Task Settings_Change_Removes_Row_And_Notifies()
    {
        var state = CreateState();
        state.HeaderMap.Should().Contain(x => x.Key == IrisKey);

        var notified = false;
        state.StateChanged += () => notified = true;

        _settings.SendIrisHeader = false;
        await SubscriptionFor(state).Consume(new SettingsModelChanged<MessagingSettings>(_settings));

        state.HeaderMap.Should().NotContain(x => x.Key == IrisKey);
        state.GetHeaders().Should().NotContainKey(IrisKey);
        notified.Should().BeTrue();
    }

    [Fact(DisplayName = "Turning the setting back on restores the iris key row exactly once")]
    public async Task Settings_Change_Restores_Row_Once()
    {
        var state = CreateState();
        var subscription = SubscriptionFor(state);

        _settings.SendIrisHeader = false;
        await subscription.Consume(new SettingsModelChanged<MessagingSettings>(_settings));

        _settings.SendIrisHeader = true;
        await subscription.Consume(new SettingsModelChanged<MessagingSettings>(_settings));
        await subscription.Consume(new SettingsModelChanged<MessagingSettings>(_settings));

        state.HeaderMap.Count(x => x.Key == IrisKey).Should().Be(1);
    }

    [Fact(DisplayName = "Disabling the iris key leaves user headers alone")]
    public async Task Disabling_Keeps_User_Headers()
    {
        var state = CreateState();
        state.AddHeader(new DictionaryViewModel { Key = "iris-correlation", Value = "mine" });
        state.AddHeader(new DictionaryViewModel { Key = "tenant", Value = "acme" });

        _settings.SendIrisHeader = false;
        await SubscriptionFor(state).Consume(new SettingsModelChanged<MessagingSettings>(_settings));

        state.GetHeaders().Should().ContainKey("iris-correlation").And.ContainKey("tenant");
        state.GetHeaders().Should().NotContainKey(IrisKey);
    }

    [Fact(DisplayName = "Disposing the state unsubscribes from settings changes")]
    public void Dispose_Unsubscribes()
    {
        var state = CreateState();

        state.Dispose();

        _bus.Received(1).Unsubscribe(Arg.Any<IConsumer<SettingsModelChanged<MessagingSettings>>>());
    }
}
