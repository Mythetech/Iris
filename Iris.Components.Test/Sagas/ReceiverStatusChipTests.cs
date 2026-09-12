using Bunit;
using FluentAssertions;
using Iris.Components.Sagas;
using Iris.Sagas;
using Iris.Telemetry;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
    private readonly IDialogService _dialogService = Substitute.For<IDialogService>();
    private readonly SagaTelemetrySettings _settings = new();

    public ReceiverStatusChipTests()
    {
        Services.AddMessageBus();
        Services.AddSingleton(_receiver);
        Services.AddSingleton(_settingsProvider);
        Services.AddSingleton(_dialogService);
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

    [Fact(DisplayName = "Shows Starting while the receiver is coming up")]
    public void Shows_Starting()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Starting);

        var cut = RenderComponent<ReceiverStatusChip>();

        cut.Markup.Should().Contain("Starting");
    }

    [Fact(DisplayName = "Counts every unmatched span, not just the bounded recent sample")]
    public async Task Shows_Cumulative_Unmatched_Count()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Stopped);
        var instances = Services.GetRequiredService<SagaInstanceState>();
        var overflow = SagaInstanceState.MaxUnmatched + 5;
        await instances.IngestAsync(Enumerable
            .Range(0, overflow)
            .Select(_ => StubSpanMapper.Span(Guid.NewGuid(), "Nowhere", "Elsewhere"))
            .ToList());
        instances.Unmatched.Count.Should().Be(SagaInstanceState.MaxUnmatched, "the sample is bounded, so the two numbers differ here");

        var cut = RenderComponent<ReceiverStatusChip>();

        cut.Markup.Should().Contain($"{overflow} unmatched");
        cut.Markup.Should().NotContain($"{SagaInstanceState.MaxUnmatched} unmatched");
    }

    [Fact(DisplayName = "Explains an out of range port when the receiver is on but nothing can listen")]
    public void Shows_Invalid_Port_Reason()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Stopped);
        _settings.ReceiverEnabled = true;
        _settings.ReceiverPort = 80;

        var cut = RenderComponent<ReceiverStatusChip>();

        cut.Markup.Should().Contain("Receiver port must be between 1024 and 65535");
    }

    [Fact(DisplayName = "Says nothing about the port range when the port is usable")]
    public void Hides_Invalid_Port_Reason_When_Port_Is_Valid()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Stopped);
        _settings.ReceiverEnabled = true;
        _settings.ReceiverPort = SagaTelemetrySettings.DefaultPort;

        var cut = RenderComponent<ReceiverStatusChip>();

        cut.Markup.Should().NotContain("must be between");
    }

    [Fact(DisplayName = "The unmatched count is a control only while something is unmatched")]
    public async Task Unmatched_Control_Appears_Only_When_Counted()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Stopped);
        var instances = Services.GetRequiredService<SagaInstanceState>();

        var before = RenderComponent<ReceiverStatusChip>();
        before.FindAll("button.receiver-unmatched").Should().BeEmpty();

        await instances.IngestAsync([StubSpanMapper.Span(Guid.NewGuid(), "Nowhere", "Elsewhere")]);
        var after = RenderComponent<ReceiverStatusChip>();

        after.FindAll("button.receiver-unmatched").Should().ContainSingle();
    }

    [Fact(DisplayName = "Clicking the unmatched count opens the unmatched transitions view")]
    public async Task Unmatched_Control_Opens_The_View()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Stopped);
        var instances = Services.GetRequiredService<SagaInstanceState>();
        await instances.IngestAsync([StubSpanMapper.Span(Guid.NewGuid(), "Nowhere", "Elsewhere")]);
        var cut = RenderComponent<ReceiverStatusChip>();

        await cut.Find("button.receiver-unmatched").ClickAsync(new MouseEventArgs());

        await _dialogService.Received(1).ShowAsync(
            typeof(UnmatchedTransitionsDialog),
            Arg.Any<string>(),
            Arg.Any<DialogOptions>());
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
