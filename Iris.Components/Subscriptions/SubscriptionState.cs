using Iris.Contracts.Subscriptions;

namespace Iris.Components.Subscriptions
{
    public class SubscriptionState
    {
        /// <summary>
        /// Constructs an instance of <see cref="SubscriptionState"/>.
        /// </summary>
        /// <param name="user">A <see cref="Subscription"/> representing the user.</param>
        public SubscriptionState(Subscription subscription)
        {
            ArgumentNullException.ThrowIfNull(subscription);
            Subscription = subscription;
        }

        /// <summary>
        /// Gets a <see cref="Subscription"/> that describes the current user.
        /// </summary>
        public Subscription Subscription { get; }
        
        public string Display
        {
            get
            {
                return Subscription switch
                {
                    null => "",
                    _ when Subscription.IsExpired => "Expired",
                    _ when Subscription.IsTrial => "Trial",
                    _ when Subscription.IsActive => "Active",
                    _ => "Inactive"
                };
            }
        }
    }
}

