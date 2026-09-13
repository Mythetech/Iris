using Iris.Contracts.Brokers.Models;

namespace Iris.Components.Endpoints;

/// <summary>
/// Read-eligibility policy shared by the Endpoints page and the Endpoints
/// panel, so the two cannot independently drift on which endpoints can be
/// read from and why not.
/// </summary>
public static class EndpointReadPolicy
{
    public static bool CanRead(EndpointDetails? endpoint, ReaderCapabilitiesDto? capabilities) =>
        endpoint is not null
        && string.Equals(endpoint.Type, "Queue", StringComparison.OrdinalIgnoreCase)
        && capabilities?.CanReceive == true;

    public static string DisabledReason(EndpointDetails? endpoint)
    {
        if (endpoint is null)
            return "Unknown endpoint";

        if (!string.Equals(endpoint.Type, "Queue", StringComparison.OrdinalIgnoreCase))
            return "Reading is only supported for queue endpoints";

        return "Broker does not support reading messages";
    }
}
