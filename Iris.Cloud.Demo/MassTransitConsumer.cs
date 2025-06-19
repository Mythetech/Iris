using System;
using System.Text.Json;
using Iris.Cloud.Demo.Contracts;
using MassTransit;

namespace Iris.Cloud.Demo
{
    public class MassTransitConsumer : IConsumer<ChangeColorsCommandV2>
    {
        public MassTransitConsumer(IConsumerNotifier<ChangeColorsCommandV2> notifier)
        {
            Notifier = notifier;
        }

        public IConsumerNotifier<ChangeColorsCommandV2> Notifier { get; }

        public async Task Consume(ConsumeContext<ChangeColorsCommandV2> context)
        {
            Console.WriteLine(JsonSerializer.Serialize(context.Message));

            await Notifier.ReceivedEvent(context.Message);
        }
    }
}

