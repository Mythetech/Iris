namespace Iris.Contracts.Brokers.Models;

/// <summary>
/// Single key/value entry in an <see cref="EndpointPropertiesDto"/>.
/// Value is nullable so adapters can surface "unset" cleanly.
/// </summary>
public sealed record EndpointPropertyEntry(string Key, string? Value);

/// <summary>
/// Broker-specific properties for an endpoint, flattened to an ordered
/// list of key/value strings for display in the UI. Adapters control
/// the order; the UI just renders the list.
/// </summary>
public sealed record EndpointPropertiesDto(IReadOnlyList<EndpointPropertyEntry> Properties);
