namespace Iris.Contracts.Brokers.Models;

/// <summary>
/// The provider names connectors report, surfaced to the UI as <see cref="Provider.Name"/>.
/// They are matched against adapter verification lists, keyed on by per-broker UI
/// registrations, and shown in compatibility copy, so they live in one place rather than
/// being spelled out per connector.
///
/// <para>
/// These sit in Contracts rather than in the broker layer because the values already cross
/// that boundary as data on every <see cref="Provider"/>: the DTO owns the field, so it
/// should also name the legal values. Consumers that cannot reference the broker assembly,
/// the Blazor component layer above all, still need the vocabulary.
/// </para>
/// </summary>
public static class ConnectorProviders
{
    public const string RabbitMq = "RabbitMq";

    public const string Azure = "Azure";

    public const string Amazon = "Amazon";

    public static IReadOnlyList<string> All { get; } = [RabbitMq, Azure, Amazon];
}
