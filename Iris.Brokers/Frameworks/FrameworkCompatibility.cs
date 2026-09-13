using Iris.Brokers.Models;

namespace Iris.Brokers.Frameworks;

public sealed record CompatibilityResult(
    bool Supported,
    string? Reason,
    IReadOnlyList<FrameworkKey> DroppedKeys);

/// <summary>
/// Matches what a framework writes against what a connection can carry. Pure: no I/O,
/// no DI, so the UI catalog and the send path share one answer. A required key the
/// transport cannot take is a failure; an optional one is dropped and reported.
/// </summary>
public static class FrameworkCompatibility
{
    /// <summary>
    /// Send-path overload: the user's own header keys are validated against the carrier
    /// before the count check, so a typo is caught locally rather than by the broker.
    /// </summary>
    public static CompatibilityResult Check(IFramework framework, IConnection connection, IReadOnlyCollection<string> userHeaderKeys)
    {
        if (connection is IHeaderCarrier carrier)
        {
            foreach (var key in userHeaderKeys)
            {
                if (!carrier.IsValidHeaderKey(key))
                    return Fail($"{connection.Connector.Provider} rejects header name '{key}'.");
            }
        }

        return Check(framework, connection, userHeaderKeys.Count);
    }

    public static CompatibilityResult Check(IFramework framework, IConnection connection, int userHeaderCount)
    {
        var provider = connection.Connector.Provider;
        var dropped = new List<FrameworkKey>();

        var headerFailure = CheckHeaders(framework, connection as IHeaderCarrier, provider, userHeaderCount, dropped);
        if (headerFailure is not null)
            return headerFailure;

        var propertyFailure = CheckTransportProperties(framework, connection as ITransportPropertyCarrier, provider, dropped);
        if (propertyFailure is not null)
            return propertyFailure;

        var declarationOrder = framework.Keys.ToList();
        return new CompatibilityResult(true, null, dropped.OrderBy(declarationOrder.IndexOf).ToList());
    }

    public static void RemoveDropped(IMessageRequest request, CompatibilityResult result)
    {
        foreach (var key in result.DroppedKeys)
        {
            if (key.Location == KeyLocation.Header)
                request.Headers.Remove(key.Name);
            else if (key.Property is { } property)
                request.TransportProperties.Clear(property);
        }
    }

    private static CompatibilityResult? CheckHeaders(
        IFramework framework, IHeaderCarrier? carrier, string provider, int userHeaderCount, List<FrameworkKey> dropped)
    {
        var headers = framework.Keys.Where(k => k.Location == KeyLocation.Header).ToList();
        if (headers.Count == 0)
            return null;

        if (carrier is null)
        {
            if (headers.Any(h => h.IsRequired))
                return Fail($"{framework.Name} needs transport headers; {provider} cannot carry them.");

            dropped.AddRange(headers);
            return null;
        }

        var kept = new List<FrameworkKey>();
        foreach (var header in headers)
        {
            if (!carrier.IsValidHeaderKey(header.Name))
            {
                if (header.IsRequired)
                    return Fail($"{provider} rejects header name '{header.Name}'.");
                dropped.Add(header);
                continue;
            }

            if (!carrier.SupportedDataTypes.Contains(header.DataType))
            {
                if (header.IsRequired)
                    return Fail($"{provider} cannot carry {header.DataType} headers ({header.Name}).");
                dropped.Add(header);
                continue;
            }

            kept.Add(header);
        }

        var requiredCount = kept.Count(h => h.IsRequired);
        if (requiredCount + userHeaderCount > carrier.MaxHeaderCount)
            return Fail(
                $"{framework.Name} needs {requiredCount} {Plural(requiredCount, "header")}"
                + (userHeaderCount > 0 ? $" plus {userHeaderCount} of yours" : string.Empty)
                + $"; {provider} allows {carrier.MaxHeaderCount}.");

        var overflow = kept.Count + userHeaderCount - carrier.MaxHeaderCount;
        for (var i = kept.Count - 1; i >= 0 && overflow > 0; i--)
        {
            if (kept[i].IsRequired)
                continue;
            dropped.Add(kept[i]);
            overflow--;
        }

        return null;
    }

    private static CompatibilityResult? CheckTransportProperties(
        IFramework framework, ITransportPropertyCarrier? carrier, string provider, List<FrameworkKey> dropped)
    {
        var properties = framework.Keys.Where(k => k.Location == KeyLocation.TransportProperty).ToList();
        if (properties.Count == 0)
            return null;

        if (carrier is null)
        {
            if (properties.Any(p => p.IsRequired))
                return Fail($"{framework.Name} needs native message properties; {provider} has none.");

            dropped.AddRange(properties);
            return null;
        }

        foreach (var property in properties)
        {
            if (carrier.SupportedProperties.Contains(property.Property!.Value))
                continue;

            if (property.IsRequired)
                return Fail($"{provider} cannot set {property.Property}.");

            dropped.Add(property);
        }

        return null;
    }

    private static string Plural(int count, string noun) => count == 1 ? noun : noun + "s";

    private static CompatibilityResult Fail(string reason) => new(false, reason, Array.Empty<FrameworkKey>());
}
