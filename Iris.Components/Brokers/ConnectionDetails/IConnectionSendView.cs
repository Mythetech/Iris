using Iris.Contracts.Brokers.Models;

namespace Iris.Components.Brokers.ConnectionDetails;

/// <summary>
/// Marker interface for a per-broker "Send" tab body. Default implementation
/// simply hosts the existing MessageEditor; brokers with bespoke send needs
/// can register their own.
/// </summary>
public interface IConnectionSendView
{
    Provider Provider { get; set; }
}
