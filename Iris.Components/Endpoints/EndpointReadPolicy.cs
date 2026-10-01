using Iris.Contracts.Brokers.Models;

namespace Iris.Components.Endpoints;

/// <summary>
/// Read-eligibility policy shared by the Endpoints page and the Endpoints
/// panel, so the two cannot independently drift on which endpoints can be
/// read from and why not.
/// </summary>
public static class EndpointReadPolicy
{
    // The kinds of endpoint a consumer reads from. A queue is one on every broker that has
    // them. A subscription is Pub/Sub's: there a topic can only be published to.
    private static readonly string[] ReadableTypes = ["Queue", "Subscription"];

    public static bool CanRead(EndpointDetails? endpoint, ReaderCapabilitiesDto? capabilities) =>
        endpoint is not null
        && IsReadable(endpoint)
        && capabilities?.CanReceive == true;

    public static string DisabledReason(EndpointDetails? endpoint)
    {
        if (endpoint is null)
            return "Unknown endpoint";

        if (!IsReadable(endpoint))
            return "Reading is only supported for queue and subscription endpoints";

        return "Broker does not support reading messages";
    }

    private static bool IsReadable(EndpointDetails endpoint) =>
        ReadableTypes.Contains(endpoint.Type, StringComparer.OrdinalIgnoreCase);
}
