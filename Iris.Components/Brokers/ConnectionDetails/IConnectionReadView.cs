using Iris.Contracts.Brokers.Models;

namespace Iris.Components.Brokers.ConnectionDetails;

/// <summary>
/// Marker interface for a per-broker "Read" tab body. Implementations are
/// responsible for rendering peek/receive controls appropriate to the broker
/// and respecting the supplied capability flags
/// (e.g., hiding DLQ controls when the broker doesn't support them).
/// </summary>
public interface IConnectionReadView
{
    Provider Provider { get; set; }
    ReaderCapabilitiesDto Capabilities { get; set; }
}
