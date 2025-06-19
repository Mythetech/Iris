using System;
namespace Iris.Contracts.Subscriptions
{
    public static class GetActiveSubscription
    {
        public record GetActiveSubscriptionResponse(bool HasActiveSubscription, Subscription? Subscription = default);
    }
}

