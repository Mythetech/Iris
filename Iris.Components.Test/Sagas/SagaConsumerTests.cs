using System.Runtime.Loader;
using Bunit;
using FluentAssertions;
using Iris.Assemblies;
using Iris.Assemblies.Messages;
using Iris.Components.Sagas;
using Iris.Components.Sagas.Consumers;
using Iris.Sagas;
using Iris.Telemetry;
using Iris.Telemetry.Messages;
using Microsoft.Extensions.DependencyInjection;
using Mythetech.Framework.Infrastructure.MessageBus;
using Mythetech.Framework.Infrastructure.Settings.Events;
using NSubstitute;

namespace Iris.Components.Test.Sagas;

public class SagaConsumerTests : IrisTestContext
{
    private readonly ISagaDefinitionProvider _provider = Substitute.For<ISagaDefinitionProvider>();
    private readonly SagaTelemetrySettings _settings = new();

    public SagaConsumerTests()
    {
        Services.AddMessageBus(typeof(SpanBatchConsumer).Assembly);
        Services.AddSingleton(_provider);
        Services.AddSingleton<ISagaSpanMapper, StubSpanMapper>();
        Services.AddSingleton<SagaDefinitionState>();
        Services.AddSingleton<SagaInstanceState>();
        Services.AddSingleton<ReceivedSpanLog>();
        Services.AddSingleton(_settings);
        Services.UseMessageBus(typeof(SpanBatchConsumer).Assembly);
    }

    private IMessageBus Bus => Services.GetRequiredService<IMessageBus>();

    [Fact(DisplayName = "AssemblyLoaded and AssemblyUnloaded flow into SagaDefinitionState")]
    public async Task Assembly_Messages_Update_Definitions()
    {
        var loaded = new LoadedAssembly { Assembly = typeof(SagaConsumerTests).Assembly, Context = AssemblyLoadContext.Default };
        var graph = new SagaGraph("A.Order", "Order", "Stub", [new SagaState("Initial", true, false)], []);
        _provider.Discover(loaded).Returns([new SagaDefinitionResult(graph, "A.Order", null)]);
        var definitions = Services.GetRequiredService<SagaDefinitionState>();

        await Bus.PublishAsync(new AssemblyLoaded(loaded));
        definitions.Graphs.Should().ContainSingle();

        await Bus.PublishAsync(new AssemblyUnloaded(loaded.Assembly.FullName!));
        definitions.Graphs.Should().BeEmpty();
    }

    [Fact(DisplayName = "SpanBatchReceived flows into SagaInstanceState")]
    public async Task Span_Batch_Updates_Instances()
    {
        var instances = Services.GetRequiredService<SagaInstanceState>();

        await Bus.PublishAsync(new SpanBatchReceived([StubSpanMapper.Span(Guid.NewGuid(), "Initial", "Nowhere")]));

        instances.SpansReceived.Should().Be(1);
    }

    [Fact(DisplayName = "SpanBatchReceived also reaches the raw log, including a span that maps to nothing")]
    public async Task Span_Batch_Reaches_The_Log()
    {
        var log = Services.GetRequiredService<ReceivedSpanLog>();
        var at = DateTimeOffset.UtcNow;
        var plain = new ReceivedSpan("trace", "span", null, "GET /orders", "svc", at, at, new Dictionary<string, string>());

        await Bus.PublishAsync(new SpanBatchReceived([plain, StubSpanMapper.Span(Guid.NewGuid(), "Initial", "Nowhere")]));

        log.Received.Should().Be(2, "the log is the only thing that can show a span Iris made nothing of");
        log.Recent.Should().HaveCount(2);
        log.Recent.Should().Contain(o => o.Span == plain && o.Result == SpanIngest.NotASagaSpan);
        log.Recent.Should().Contain(o => o.Result == SpanIngest.Unmatched);
    }

    [Fact(DisplayName = "Enabling the setting publishes StartOtlpReceiver with the configured port")]
    public async Task Settings_Enabled_Starts_Receiver()
    {
        var started = new List<StartOtlpReceiver>();
        Bus.Subscribe(new LambdaConsumer<StartOtlpReceiver>(started.Add));
        _settings.ReceiverEnabled = true;
        _settings.ReceiverPort = 4400;

        await Bus.PublishAsync(new SettingsModelChanged<SagaTelemetrySettings>(_settings));

        started.Should().ContainSingle(m => m.Port == 4400);
    }

    [Fact(DisplayName = "Disabling the setting publishes StopOtlpReceiver")]
    public async Task Settings_Disabled_Stops_Receiver()
    {
        var stopped = new List<StopOtlpReceiver>();
        Bus.Subscribe(new LambdaConsumer<StopOtlpReceiver>(stopped.Add));
        _settings.ReceiverEnabled = false;

        await Bus.PublishAsync(new SettingsModelChanged<SagaTelemetrySettings>(_settings));

        stopped.Should().ContainSingle();
    }

    private sealed class LambdaConsumer<T>(Action<T> onMessage) : IConsumer<T> where T : class
    {
        public Task Consume(T message)
        {
            onMessage(message);
            return Task.CompletedTask;
        }
    }
}
