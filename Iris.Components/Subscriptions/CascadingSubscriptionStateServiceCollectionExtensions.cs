using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Components.Subscriptions
{

    /// <summary>
    /// Extension methods for configuring cascading Subscription state on a service collection.
    /// </summary>
    public static class CascadingSubscriptionStateServiceCollectionExtensions
    {
        /// <summary>
        /// Adds cascading Subscription state to the <paramref name="serviceCollection"/>. This is equivalent to
        /// having a <see cref="CascadingSubscriptionState"/> component at the root of your component hierarchy.
        /// </summary>
        /// <param name="serviceCollection">The <see cref="IServiceCollection"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddCascadingSubscriptionState(this IServiceCollection serviceCollection)
        {
            return serviceCollection.AddCascadingValue<Task<SubscriptionState>>(services =>
            {
                var subscriptionStateProvider = services.GetRequiredService<SubscriptionStateProvider>();
                return new SubscriptionStateCascadingValueSource(subscriptionStateProvider);
            });
        }

        private sealed class SubscriptionStateCascadingValueSource : CascadingValueSource<Task<SubscriptionState>>, IDisposable
        {
            // This is intended to produce identical behavior to having a <CascadingSubscriptionStateProvider>
            // wrapped around the root component.

            private readonly SubscriptionStateProvider _SubscriptionStateProvider;

            public SubscriptionStateCascadingValueSource(SubscriptionStateProvider SubscriptionStateProvider)
                : base(SubscriptionStateProvider.GetSubscriptionStateAsync, isFixed: false)
            {
                _SubscriptionStateProvider = SubscriptionStateProvider;
                _SubscriptionStateProvider.SubscriptionStateChanged += HandleSubscriptionStateChanged;
            }

            private void HandleSubscriptionStateChanged(Task<SubscriptionState> newAuthStateTask)
            {
                _ = NotifyChangedAsync(newAuthStateTask);
            }

            public void Dispose()
            {
                _SubscriptionStateProvider.SubscriptionStateChanged -= HandleSubscriptionStateChanged;
            }
        }
    }
}

