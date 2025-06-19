using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Iris.Components.Subscriptions
{
    public abstract class SubscriptionViewCore : ComponentBase
    {
        private SubscriptionState? currentSubscriptionState;
        private bool? isSubscribed;

        /// <summary>
        /// The content that will be displayed if the user is Subscribed.
        /// </summary>
        [Parameter] public RenderFragment<SubscriptionState>? ChildContent { get; set; }

        /// <summary>
        /// The content that will be displayed if the user is not Subscribed.
        /// </summary>
        [Parameter] public RenderFragment<SubscriptionState>? NotSubscribed { get; set; }

        /// <summary>
        /// The content that will be displayed if the user is Subscribed.
        /// If you specify a value for this parameter, do not also specify a value for <see cref="ChildContent"/>.
        /// </summary>
        [Parameter] public RenderFragment<SubscriptionState>? Subscribed { get; set; }

        /// <summary>
        /// The content that will be displayed while asynchronous subscription check is in progress.
        /// </summary>
        [Parameter] public RenderFragment? Loading { get; set; }

        /// <summary>
        /// The resource to which access is being controlled.
        /// </summary>
        [Parameter] public object? Resource { get; set; }

        [CascadingParameter] private Task<SubscriptionState>? SubscriptionState { get; set; }


        [Inject] private SubscriptionStateProvider SubscriptionService { get; set; } = default!;

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            // We're using the same sequence number for each of the content items here
            // so that we can update existing instances if they are the same shape
            if (isSubscribed == null)
            {
                builder.AddContent(0, Loading);
            }
            else if (isSubscribed == true)
            {
                var subscribed = Subscribed ?? ChildContent;
                builder.AddContent(0, Subscribed?.Invoke(currentSubscriptionState!));
            }
            else
            {
                builder.AddContent(0, NotSubscribed?.Invoke(currentSubscriptionState!));
            }
        }

        /// <inheritdoc />
        protected override async Task OnParametersSetAsync()
        {
            // We allow 'ChildContent' for convenience in basic cases, and 'Subscribed' for symmetry
            // with 'NotSubscribed' in other cases. Besides naming, they are equivalent. To avoid
            // confusion, explicitly prevent the case where both are supplied.
            if (ChildContent != null && Subscribed != null)
            {
                throw new InvalidOperationException($"Do not specify both '{nameof(Subscribed)}' and '{nameof(ChildContent)}'.");
            }

            if (SubscriptionState == null)
            {
                throw new InvalidOperationException($"Subscription requires a cascading parameter of type Task<{nameof(SubscriptionState)}>. Consider using {typeof(CascadingSubscriptionState).Name} to supply this.");
            }

            // Clear the previous result of authorization
            // This will cause the Authorizing state to be displayed until the authorization has been completed
            isSubscribed = null;

            currentSubscriptionState = await SubscriptionState;
            isSubscribed = await IsSubscribedAsync();
        }

        private async Task<bool> IsSubscribedAsync()
        {
            var result = await SubscriptionService.GetSubscriptionStateAsync();
            return result.Subscription.IsActive;
        }

    }
}

