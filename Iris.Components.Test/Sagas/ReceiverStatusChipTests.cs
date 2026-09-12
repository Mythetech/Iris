using Bunit;
using FluentAssertions;
using Iris.Components.Sagas;
using Iris.Sagas;
using Iris.Telemetry;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;
using Mythetech.Framework.Infrastructure.Settings;
using NSubstitute;

namespace Iris.Components.Test.Sagas;

public class ReceiverStatusChipTests : IrisTestContext
{
    private readonly IOtlpReceiver _receiver = Substitute.For<IOtlpReceiver>();
    private readonly ISettingsProvider _settingsProvider = Substitute.For<ISettingsProvider>();
    private readonly SagaTelemetrySettings _settings = new();

    public ReceiverStatusChipTests()
    {
        Services.AddMessageBus();
        Services.AddSingleton(_receiver);
        Services.AddSingleton(_settingsProvider);
        Services.AddSingleton(_settings);
        Services.AddSingleton(Substitute.For<ISagaDefinitionProvider>());
        Services.AddSingleton<ISagaSpanMapper, StubSpanMapper>();
        Services.AddSingleton<SagaDefinitionState>();
        Services.AddSingleton<SagaInstanceState>();

        RenderComponent<MudPopoverProvider>();
    }

    [Fact(DisplayName = "Shows Stopped when the receiver is not listening")]
    public void Shows_Stopped()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Stopped);

        var cut = RenderComponent<ReceiverStatusChip>();

        cut.Markup.Should().Contain("Stopped");
    }

    [Fact(DisplayName = "Shows the endpoint and env var snippet when listening")]
    public void Shows_Endpoint_When_Listening()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Listening);
        _receiver.Endpoint.Returns("http://127.0.0.1:4318");

        var cut = RenderComponent<ReceiverStatusChip>();

        cut.Markup.Should().Contain("http://127.0.0.1:4318");
        cut.Markup.Should().Contain("OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4318");
        cut.Markup.Should().Contain("OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf");
    }

    [Fact(DisplayName = "Shows the error when the receiver failed")]
    public void Shows_Failure()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Failed);
        _receiver.LastError.Returns("address in use");

        var cut = RenderComponent<ReceiverStatusChip>();

        cut.Markup.Should().Contain("Failed").And.Contain("address in use");
    }

    [Fact(DisplayName = "Toggling updates the setting and notifies the settings provider")]
    public async Task Toggle_Updates_Setting()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Stopped);
        var cut = RenderComponent<ReceiverStatusChip>();

        await cut.Find("input[type=checkbox]").ChangeAsync(new ChangeEventArgs { Value = true });

        _settings.ReceiverEnabled.Should().BeTrue();
        await _settingsProvider.Received(1).NotifySettingsChangedAsync(_settings);
    }
}
