using Iris.Components.Subscriptions;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Components.Test.Subscriptions;

public class TestSubscriptionService : SubscriptionStateProvider, ISubscriptionService
{
    private bool _validSubscription = false;
    
    public void SetSubscribed() => _validSubscription = true;
    
    public Task<bool> HasValidSubscriptionAsync()
    {
        return Task.FromResult<bool>(_validSubscription); 
    }

    public Task ActivateTrialAsync()
    {
        _validSubscription = true;
        return Task.CompletedTask;
    }

    public override Task<SubscriptionState> GetSubscriptionStateAsync()
    {
        return Task.FromResult(new SubscriptionState(new()
        {
           IsActive = _validSubscription, 
           IsTrial = _validSubscription, 
           IsExpired = !_validSubscription 
        }));
    }
}

public static class TestSubscriptionServiceExtensions
{
    public static TestSubscriptionService AddTestSubscriptionService(this IServiceCollection services)
    {
        var service = new TestSubscriptionService();
        services.AddSingleton<ISubscriptionService>(service);
        services.AddSingleton<SubscriptionStateProvider>(service);
        services.AddCascadingSubscriptionState();
        return service;
    }
}