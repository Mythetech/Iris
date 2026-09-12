using Iris.Telemetry.Messages;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Iris.Components.Sagas.Consumers;

/// <summary>
/// The composition point for an arriving batch: the instance state folds the spans it understands
/// into live instances and reports what it made of each one, and the log keeps the whole batch so
/// the spans that mapped to nothing are still visible.
/// </summary>
public sealed class SpanBatchConsumer : IConsumer<SpanBatchReceived>
{
    private readonly SagaInstanceState _instances;
    private readonly ReceivedSpanLog _log;

    public SpanBatchConsumer(SagaInstanceState instances, ReceivedSpanLog log)
    {
        _instances = instances;
        _log = log;
    }

    public async Task Consume(SpanBatchReceived message)
    {
        var outcomes = await _instances.IngestAsync(message.Spans);
        await _log.RecordAsync(outcomes);
    }
}
