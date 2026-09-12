using Iris.Telemetry.Messages;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Iris.Components.Sagas.Consumers;

public sealed class SpanBatchConsumer : IConsumer<SpanBatchReceived>
{
    private readonly SagaInstanceState _instances;

    public SpanBatchConsumer(SagaInstanceState instances)
    {
        _instances = instances;
    }

    public Task Consume(SpanBatchReceived message) => _instances.IngestAsync(message.Spans);
}
