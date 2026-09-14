namespace Iris.Contracts.Brokers.Models;

/// <summary>
/// The transport names connections report, surfaced to the UI as
/// <see cref="Provider.Transport"/>.
///
/// <para>
/// A transport is finer than a <see cref="ConnectorProviders">provider</see>: one Azure
/// connector reports <c>Azure</c> for both Azure Service Bus and Azure Queue Storage, which
/// speak different wire formats. Anything that reasons about what is on the wire has to key
/// off this, not off the provider.
/// </para>
///
/// <para>
/// Not every connection reports a stable one. The RabbitMQ connection overwrites its name
/// with <c>Docker</c> or <c>CloudAmpq</c> depending on the address, so a consumer that needs
/// to identify a RabbitMQ connection falls back to the provider name. RabbitMQ can afford
/// that: it has one transport, so the provider already pins the wire format.
/// </para>
/// </summary>
public static class ConnectorTransports
{
    public const string RabbitMq = "RabbitMq";

    public const string AzureServiceBus = "AzureServiceBus";

    public const string AzureQueueStorage = "AzureQueueStorage";

    public const string SimpleQueueService = "SimpleQueueService";
}
