using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;

namespace Iris.Components.Infrastructure;

/// <summary>
/// Maps a normalized broker key to the component <see cref="Type"/> that renders
/// <typeparamref name="TContract"/> for that broker, for dispatch through
/// <c>DynamicComponent</c>.
///
/// <para>
/// The registry is the UI-side counterpart to the capability interfaces in
/// <c>Iris.Brokers</c>: a broker opts into a bespoke view by registering one, and
/// everything else falls back to the default. Adding a broker therefore needs no
/// change to any component that renders it.
/// </para>
///
/// <para>
/// <see cref="Register{TComponent}"/> is the only way in, so the compiler enforces
/// that a registered type is both a component and an implementation of
/// <typeparamref name="TContract"/>. That keeps the unchecked cast out of the
/// render path: a resolved type is known to satisfy the contract.
/// </para>
/// </summary>
/// <typeparam name="TContract">
/// The view contract the registered component must implement, for example
/// <c>IConnectionEndpointsView</c>.
/// </typeparam>
public sealed class ComponentRegistry<TContract>
{
    private readonly Dictionary<string, Type> _components = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers <typeparamref name="TComponent"/> as the view for <paramref name="key"/>,
    /// which is normalized before storage so callers can pass a broker's display spelling.
    /// </summary>
    public ComponentRegistry<TContract> Register<TComponent>(string key)
        where TComponent : IComponent, TContract
    {
        var normalized = Normalize(key);
        if (normalized.Length == 0)
            throw new ArgumentException("Registry key must contain at least one alphanumeric character.", nameof(key));

        _components[normalized] = typeof(TComponent);
        return this;
    }

    /// <summary>
    /// Resolves the first <paramref name="candidateKeys"/> entry with a registered view.
    ///
    /// <para>
    /// Callers pass more than one key because the broker model produces them in two
    /// shapes. Both Azure adapters report <c>IConnector.Provider == "Azure"</c>, so only
    /// the transport separates Azure Service Bus from Azure Queue Storage, which is why
    /// transport is tried first. RabbitMq goes the other way: its <c>IConnection.Name</c>
    /// becomes "Docker" or "CloudAmpq" depending on the address, values no registry would
    /// sensibly key off, so the fall back to the provider name keeps every RabbitMq
    /// instance on the right view. Register a broker under whichever of the two is stable
    /// across its instances.
    /// </para>
    /// </summary>
    public bool TryResolve([NotNullWhen(true)] out Type? componentType, params string?[] candidateKeys)
    {
        foreach (var key in candidateKeys)
        {
            var normalized = Normalize(key);
            if (normalized.Length > 0 && _components.TryGetValue(normalized, out componentType))
                return true;
        }

        componentType = null;
        return false;
    }

    /// <inheritdoc cref="TryResolve"/>
    /// <summary>
    /// Resolves a view for the first matching key, or <paramref name="fallback"/> when
    /// no candidate is registered.
    /// </summary>
    public Type Resolve(Type fallback, params string?[] candidateKeys)
        => TryResolve(out var componentType, candidateKeys) ? componentType : fallback;

    /// <summary>
    /// Reduces a broker name or transport to its registry key: alphanumerics only,
    /// lower-cased invariantly. "Azure Service Bus" and "azureservicebus" collapse
    /// to the same key.
    /// </summary>
    public static string Normalize(string? value)
        => new([.. (value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);
}
