namespace Iris.Components.Subscriptions
{
    /// <summary>
    /// Provides information about the Subscription state of the current user.
    /// </summary>
    public abstract class SubscriptionStateProvider
    {
        /// <summary>
        /// Asynchronously gets an <see cref="SubscriptionState"/> that describes the current user.
        /// </summary>
        /// <returns>A task that, when resolved, gives an <see cref="SubscriptionState"/> instance that describes the current user.</returns>
        public abstract Task<SubscriptionState> GetSubscriptionStateAsync();

        /// <summary>
        /// An event that provides notification when the <see cref="SubscriptionState"/>
        /// has changed. For example, this event may be raised if a user logs in or out.
        /// </summary>
        public event SubscriptionStateChangedHandler? SubscriptionStateChanged;

        /// <summary>
        /// Raises the <see cref="SubscriptionStateChanged"/> event.
        /// </summary>
        /// <param name="task">A <see cref="Task"/> that supplies the updated <see cref="SubscriptionState"/>.</param>
        protected void NotifySubscriptionStateChanged(Task<SubscriptionState> task)
        {
            ArgumentNullException.ThrowIfNull(task);

            SubscriptionStateChanged?.Invoke(task);
        }
    }

    /// <summary>
    /// A handler for the <see cref="SubscriptionStateProvider.SubscriptionStateChanged"/> event.
    /// </summary>
    /// <param name="task">A <see cref="Task"/> that supplies the updated <see cref="SubscriptionState"/>.</param>
    public delegate void SubscriptionStateChangedHandler(Task<SubscriptionState> task);
}

