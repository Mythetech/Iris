using System.Text.Json;
using Iris.Cloud.Demo.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Iris.Cloud.Demo
{
    public class NServiceBusConsumer : IHandleMessages<ChangeColorsCommand>
    {
        private readonly ILogger<NServiceBusConsumer> _logger;

        public NServiceBusConsumer(IConsumerNotifier<ChangeColorsCommand> notifier, ILogger<NServiceBusConsumer> logger)
        {
            Notifier = notifier;
            _logger = logger;
        }

        public IServiceProvider ServiceProvider { get; }
        public IConsumerNotifier<ChangeColorsCommand> Notifier { get; }

        public async Task Handle(ChangeColorsCommand message, IMessageHandlerContext context)
        {
            _logger.LogDebug("Received message: {Message}", JsonSerializer.Serialize(message));

            await Notifier.ReceivedEvent(message);
        }
    }
}

