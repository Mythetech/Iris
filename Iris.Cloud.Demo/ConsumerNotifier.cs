using System;
using Iris.Cloud.Demo.Contracts;

namespace Iris.Cloud.Demo
{
    public interface IConsumerNotifier<T>
    {
        public Task ReceivedEvent(T args);
    }

    public class ChangeColorsCommandV2Notifier : IConsumerNotifier<ChangeColorsCommandV2>
    {
        public Action<ChangeColorsCommandV2> HandleEvent;

        public Task ReceivedEvent(ChangeColorsCommandV2 args)
        {
            HandleEvent?.Invoke(args);
            return Task.CompletedTask;
        }
    }
}

