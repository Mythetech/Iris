namespace Iris.Brokers;

/// <summary>
/// Capability interface for broker connections that can return rich
/// per-endpoint metadata (message counts, durability flags, bindings,
/// runtime stats, ...).
/// </summary>
/// <remarks>
/// <para>
/// Like the read capabilities in <see cref="IMessageReader"/>, this is
/// opt-in: a broker that can't expose endpoint metadata simply does not
/// implement <see cref="IEndpointInspector"/>, and the service layer
/// returns <c>null</c> from <c>GetEndpointPropertiesAsync</c>. The UI
/// treats that as "properties not available for this broker."
/// </para>
/// <para>
/// Adapters are responsible for flattening their native types into
/// strings — bindings get joined, timestamps get formatted, dictionaries
/// get rendered. This keeps broker-specific types out of
/// <c>Iris.Contracts</c> and lets the UI stay dumb.
/// </para>
/// </remarks>
public interface IEndpointInspector
{
    /// <summary>
    /// Inspect the endpoint at <paramref name="endpointName"/> and return
    /// an ordered list of broker-specific key/value properties suitable
    /// for display.
    /// </summary>
    /// <param name="endpointName">Name of the endpoint to inspect.</param>
    /// <param name="type">
    /// Endpoint type hint from the discovery pass (e.g. <c>"Queue"</c>,
    /// <c>"Topic"</c>, <c>"Exchange"</c>). Adapters can use this to pick
    /// between native API calls. May be <c>null</c>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Iris.Contracts.Brokers.Models.EndpointPropertiesDto> InspectAsync(
        string endpointName,
        string? type,
        CancellationToken cancellationToken = default);
}
