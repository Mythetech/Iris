using Bunit;
using MudBlazor;

namespace Iris.Components.Test.Subscriptions;

public class SubscriptionViewTests : IrisTestContext
{
    private TestSubscriptionService _subscriptionService;
    public SubscriptionViewTests()
    {
        _subscriptionService = Services.AddTestSubscriptionService();
        this.RenderComponent<MudPopoverProvider>();
    }

    [Fact(DisplayName = "Shows unsubscribed view if no valid subscription")]
    public void Shows_Unsubscribed_Content()
    {
        // Act
        var cut = RenderComponent<TestSubscriptionContent>();

        // Assert
        cut.MarkupMatches("<p>No Subscription</p>");
    }
    
    [Fact(DisplayName = "Shows subscribed view if valid subscription")]
    public void Shows_Subscribed_Content()
    {
        // Arrange
        _subscriptionService.SetSubscribed();
        
        // Act
        var cut = RenderComponent<TestSubscriptionContent>();

        // Assert
        cut.MarkupMatches("<p>Valid Subscription</p>");
    }
}