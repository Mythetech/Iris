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

        Render<MudPopoverProvider>();
    }

    [Fact(DisplayName = "Shows Stopped when the receiver is not listening")]
    public void Shows_Stopped()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Stopped);

        var cut = Render<ReceiverStatusChip>();

        cut.Markup.Should().Contain("Stopped");
    }

    [Fact(DisplayName = "Shows the endpoint and env var snippet when listening")]
    public void Shows_Endpoint_When_Listening()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Listening);
        _receiver.Endpoint.Returns("http://127.0.0.1:4318");

        var cut = Render<ReceiverStatusChip>();

        cut.Markup.Should().Contain("http://127.0.0.1:4318");
        cut.Markup.Should().Contain("OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4318");
        cut.Markup.Should().Contain("OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf");
    }

    [Fact(DisplayName = "Shows Starting while the receiver is coming up")]
    public void Shows_Starting()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Starting);

        var cut = Render<ReceiverStatusChip>();

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

        var cut = Render<ReceiverStatusChip>();

        cut.Markup.Should().Contain($"{overflow} unmatched");
        cut.Markup.Should().NotContain($"{SagaInstanceState.MaxUnmatched} unmatched");
    }

    [Fact(DisplayName = "Explains an out of range port when the receiver is on but nothing can listen")]
    public void Shows_Invalid_Port_Reason()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Stopped);
        _settings.ReceiverEnabled = true;
        _settings.ReceiverPort = 80;

        var cut = Render<ReceiverStatusChip>();

        cut.Markup.Should().Contain("Receiver port must be between 1024 and 65535");
    }

    [Fact(DisplayName = "Says nothing about the port range when the port is usable")]
    public void Hides_Invalid_Port_Reason_When_Port_Is_Valid()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Stopped);
        _settings.ReceiverEnabled = true;
        _settings.ReceiverPort = SagaTelemetrySettings.DefaultPort;

        var cut = Render<ReceiverStatusChip>();

        cut.Markup.Should().NotContain("must be between");
    }

    [Fact(DisplayName = "The unmatched count is a control only while something is unmatched")]
    public async Task Unmatched_Control_Appears_Only_When_Counted()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Stopped);
        var instances = Services.GetRequiredService<SagaInstanceState>();

        var before = Render<ReceiverStatusChip>();
        before.FindAll("button.receiver-unmatched").Should().BeEmpty();

        await instances.IngestAsync([StubSpanMapper.Span(Guid.NewGuid(), "Nowhere", "Elsewhere")]);
        var after = Render<ReceiverStatusChip>();

        after.FindAll("button.receiver-unmatched").Should().ContainSingle();
    }

    [Fact(DisplayName = "Clicking the unmatched count opens the unmatched transitions view")]
    public async Task Unmatched_Control_Opens_The_View()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Stopped);
        var instances = Services.GetRequiredService<SagaInstanceState>();
        await instances.IngestAsync([StubSpanMapper.Span(Guid.NewGuid(), "Nowhere", "Elsewhere")]);
        var cut = Render<ReceiverStatusChip>();

        await cut.Find("button.receiver-unmatched").ClickAsync(new MouseEventArgs());

        await _dialogService.Received(1).ShowAsync(
            typeof(UnmatchedTransitionsDialog),
            Arg.Any<string>(),
            Arg.Any<DialogOptions>());
    }

    [Fact(DisplayName = "A chip already on screen follows the span counts as they climb")]
    public async Task Counts_Follow_Ingest_Without_A_Fresh_Render()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Listening);
        _receiver.Endpoint.Returns("http://127.0.0.1:4318");
        var instances = Services.GetRequiredService<SagaInstanceState>();
        var cut = Render<ReceiverStatusChip>();
        cut.Find(".receiver-counts").TextContent.Trim().Should().Be("0 spans, 0 mapped");

        await instances.IngestAsync([StubSpanMapper.Span(Guid.NewGuid(), "Nowhere", "Elsewhere")]);

        cut.Find(".receiver-counts").TextContent.Trim().Should().Be("1 spans, 1 mapped",
            "the page never re-creates this chip, so it has to follow the state it reads rather than wait for a parent render");
    }

    [Fact(DisplayName = "A chip already on screen follows the receiver coming up")]
    public async Task Status_Follows_The_Receiver_Without_A_Fresh_Render()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Stopped);
        var cut = Render<ReceiverStatusChip>();
        cut.Markup.Should().Contain("Stopped");

        _receiver.Status.Returns(OtlpReceiverStatus.Listening);
        _receiver.Endpoint.Returns("http://127.0.0.1:4318");
        await Services.GetRequiredService<IMessageBus>()
            .PublishAsync(new Iris.Telemetry.Messages.OtlpReceiverStatusChanged(
                OtlpReceiverStatus.Listening, "http://127.0.0.1:4318", null));

        cut.Markup.Should().Contain("Listening").And.Contain("http://127.0.0.1:4318");
        cut.Markup.Should().NotContain("Stopped");
    }

    [Fact(DisplayName = "The copy control carries one tooltip rather than a second stacked over it")]
    public void Copy_Control_Has_A_Single_Tooltip()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Listening);
        _receiver.Endpoint.Returns("http://127.0.0.1:4318");

        var cut = Render<ReceiverStatusChip>();

        cut.FindAll(".mud-tooltip-root").Should().ContainSingle(
            "MtCopyButton brings its own tooltip, which also swaps to Copied on success, so wrapping it in another one renders both at once");
    }

    [Fact(DisplayName = "The span counts open the raw received spans view")]
    public async Task Counts_Open_The_Received_Spans_View()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Stopped);
        var cut = Render<ReceiverStatusChip>();

        await cut.Find("button.receiver-counts").ClickAsync(new MouseEventArgs());

        await _dialogService.Received(1).ShowAsync(
            typeof(ReceivedSpansDialog),
            Arg.Any<string>(),
            Arg.Any<DialogOptions>());
    }

    [Fact(DisplayName = "The counts stay reachable before any span arrives, when the view is most needed")]
    public void Counts_Are_Reachable_While_Empty()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Listening);
        _receiver.Endpoint.Returns("http://127.0.0.1:4318");

        var cut = Render<ReceiverStatusChip>();

        cut.FindAll("button.receiver-counts").Should().ContainSingle(
            "an empty feed is exactly what tells you the traces are not arriving");
    }

    [Fact(DisplayName = "Shows the error when the receiver failed")]
    public void Shows_Failure()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Failed);
        _receiver.LastError.Returns("address in use");

        var cut = Render<ReceiverStatusChip>();

        cut.Markup.Should().Contain("Failed").And.Contain("address in use");
    }

    [Fact(DisplayName = "Toggling updates the setting and notifies the settings provider")]
    public async Task Toggle_Updates_Setting()
    {
        _receiver.Status.Returns(OtlpReceiverStatus.Stopped);
        var cut = Render<ReceiverStatusChip>();

        await cut.Find("input[type=checkbox]").ChangeAsync(new ChangeEventArgs { Value = true });

        _settings.ReceiverEnabled.Should().BeTrue();
        await _settingsProvider.Received(1).NotifySettingsChangedAsync(_settings);
    }
}
