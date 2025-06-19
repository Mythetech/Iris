using System;
using System.Text.Json;
using Iris.Cloud.Demo.Contracts;
using MassTransit;

namespace Iris.Cloud.Demo
{
    public class NServiceBusConsumer : IHandleMessages<ChangeColorsCommand>
    {
        public NServiceBusConsumer(IConsumerNotifier<ChangeColorsCommand> notifier)
        {
            Notifier = notifier;
        }

        public IServiceProvider ServiceProvider { get; }
        public IConsumerNotifier<ChangeColorsCommand> Notifier { get; }

        public async Task Handle(ChangeColorsCommand message, IMessageHandlerContext context)
        {
            Console.WriteLine(JsonSerializer.Serialize(message));

            await Notifier.ReceivedEvent(message);
        }
    }
}

