namespace Iris.Components.Subscriptions
{
    public interface ISubscriptionService
    {
        public Task<bool> HasValidSubscriptionAsync();

        public Task ActivateTrialAsync();
    }
}

