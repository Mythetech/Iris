using Iris.Telemetry;
using Iris.Telemetry.Messages;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Iris.Desktop.Telemetry;

/// <summary>Bridges the receiver, which knows nothing about the bus, onto the bus.</summary>
public sealed class MessageBusTelemetrySink : ITelemetrySink
{
    private readonly IMessageBus _bus;

    public MessageBusTelemetrySink(IMessageBus bus)
    {
        _bus = bus;
    }

    public Task AcceptAsync(IReadOnlyList<ReceivedSpan> spans, CancellationToken cancellationToken)
        => _bus.PublishAsync(new SpanBatchReceived(spans));

    public Task StatusChangedAsync(OtlpReceiverStatus status, string? endpoint, string? error, CancellationToken cancellationToken)
        => _bus.PublishAsync(new OtlpReceiverStatusChanged(status, endpoint, error));
}
