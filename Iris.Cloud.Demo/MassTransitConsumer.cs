using System.Text.Json;
using Iris.Cloud.Demo.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Iris.Cloud.Demo
{
    public class MassTransitConsumer : IConsumer<ChangeColorsCommandV2>
    {
        private readonly ILogger<MassTransitConsumer> _logger;

        public MassTransitConsumer(IConsumerNotifier<ChangeColorsCommandV2> notifier, ILogger<MassTransitConsumer> logger)
        {
            Notifier = notifier;
            _logger = logger;
        }

        public IConsumerNotifier<ChangeColorsCommandV2> Notifier { get; }

        public async Task Consume(ConsumeContext<ChangeColorsCommandV2> context)
        {
            _logger.LogDebug("Received message: {Message}", JsonSerializer.Serialize(context.Message));

            await Notifier.ReceivedEvent(context.Message);
        }
    }
}

