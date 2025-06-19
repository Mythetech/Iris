using Iris.Cloud.Demo.Contracts;

namespace Iris.Cloud.Demo
{
    public class ChangeColorsCommandNotifier : IConsumerNotifier<ChangeColorsCommand>
    {
        public Action<ChangeColorsCommand> HandleEvent;

        public Task ReceivedEvent(ChangeColorsCommand args)
        {
            HandleEvent?.Invoke(args);
            return Task.CompletedTask;
        }
    }
}

