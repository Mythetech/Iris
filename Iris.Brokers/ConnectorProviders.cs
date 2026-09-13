namespace Iris.Brokers;

/// <summary>
/// The provider names connectors report through <see cref="IConnector.Provider"/>. They are
/// matched against adapter verification lists and shown in compatibility copy, so they live
/// in one place rather than being spelled out per connector.
/// </summary>
public static class ConnectorProviders
{
    public const string RabbitMq = "RabbitMq";

    public const string Azure = "Azure";

    public const string Amazon = "Amazon";

    public static IReadOnlyList<string> All { get; } = [RabbitMq, Azure, Amazon];
}
