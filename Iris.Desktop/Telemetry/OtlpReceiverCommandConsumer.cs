using Iris.Telemetry;
using Iris.Telemetry.Messages;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Iris.Desktop.Telemetry;

public sealed class OtlpReceiverCommandConsumer : IConsumer<StartOtlpReceiver>, IConsumer<StopOtlpReceiver>
{
    private readonly IOtlpReceiver _receiver;

    public OtlpReceiverCommandConsumer(IOtlpReceiver receiver)
    {
        _receiver = receiver;
    }

    public Task Consume(StartOtlpReceiver message) => _receiver.StartAsync(message.Port);

    public Task Consume(StopOtlpReceiver message) => _receiver.StopAsync();
}
