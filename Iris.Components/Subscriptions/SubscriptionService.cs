using System.Net.Http.Json;
using System.Text.Json;
using Iris.Contracts.Subscriptions;
using Microsoft.AspNetCore.Components.Authorization;

namespace Iris.Components.Subscriptions
{
    public class SubscriptionService : SubscriptionStateProvider, ISubscriptionService
    {
        private readonly AuthenticationStateProvider _authProvider;
        private readonly HttpClient _client;

        private SubscriptionState? _cachedState = default!;

        private Subscription _unsubscribedSubscription = new()
        {
            IsActive = false,
            CustomerId = "Unauthorized"
        };

        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public SubscriptionService(IHttpClientFactory factory, AuthenticationStateProvider authProvider)
        {
            _authProvider = authProvider;
            _client = factory.CreateClient("Auth");
        }

        public override async Task<SubscriptionState> GetSubscriptionStateAsync()
        {
            var authState = await _authProvider.GetAuthenticationStateAsync();
            bool unauthorized = !authState.User.Identity?.IsAuthenticated ?? true;
            
            if (unauthorized)
            {
                return new SubscriptionState(_unsubscribedSubscription);
            }
            
            if (_cachedState != null && _cachedState.Subscription.IsActive)
                return _cachedState;

            var subscription = await GetSubscriptionAsync();

            var state = new SubscriptionState(new Subscription()
            {
                IsActive = subscription.IsActive,
                CustomerId = "",
                IsExpired = subscription.IsExpired,
                IsTrial = subscription.IsTrial
            });

            _cachedState = state;

            return _cachedState;
        }
        
        private async Task<Subscription> GetSubscriptionAsync()
        {
            var resp = await _client.GetFromJsonAsync<GetActiveSubscription.GetActiveSubscriptionResponse>("/api/subscriptions/active");

            return resp?.Subscription ?? new();
        }

        public async Task<bool> HasValidSubscriptionAsync()
        {
            var resp = await _client.GetFromJsonAsync<GetActiveSubscription.GetActiveSubscriptionResponse>("/api/subscriptions/active");

            return resp?.HasActiveSubscription ?? false;
        }

        public async Task ActivateTrialAsync()
        {
            var resp = await _client.PostAsJsonAsync("/api/subscriptions/trial", "");

            if (resp.IsSuccessStatusCode)
            {
                await GetSubscriptionStateAsync();
            }
        }
    }
}

