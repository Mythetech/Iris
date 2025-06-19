namespace Iris.Contracts.Subscriptions.Commands;

public record KeyActivateSubscription(string Email, Guid SubscriptionKey);