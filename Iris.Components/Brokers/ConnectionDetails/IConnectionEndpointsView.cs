using Iris.Contracts.Brokers.Models;

namespace Iris.Components.Brokers.ConnectionDetails;

/// <summary>
/// Marker interface for a per-broker "Endpoints" tab body. Implementations
/// receive the current <see cref="Provider"/> as a parameter and render the
/// endpoint listing using broker-native vocabulary
/// (Queues + Exchanges, Queues + Topics + Subscriptions, etc.).
/// </summary>
public interface IConnectionEndpointsView
{
    Provider Provider { get; set; }
}
