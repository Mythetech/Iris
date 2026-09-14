using Iris.Contracts.Messaging.Frameworks;

namespace Iris.Brokers.Frameworks
{
    public interface IFramework
    {
        public string Name { get; }

        /// <summary>Everything this adapter writes, by location. See <see cref="FrameworkKey"/>.</summary>
        public IReadOnlyList<FrameworkKey> Keys { get; }

        /// <summary>UI-facing projection of this adapter's name and the inputs it wants.</summary>
        public FrameworkDescriptor Descriptor { get; }

        /// <summary>
        /// Where this adapter has a consumer round-trip test. The key list describes one wire
        /// shape, read from the framework's RabbitMQ receive pipeline; other transports may
        /// spell the same metadata differently, so a target that is absent here still sends
        /// but is flagged as unverified in the UI.
        ///
        /// <para>
        /// Entries are matched by <c>LocalFrameworkCatalog.IsVerified</c> against a
        /// connection's <see cref="IConnection.Name">transport</see> first and its
        /// <see cref="IConnector.Provider">provider</see> second, so claim at whichever
        /// granularity actually pins the wire format. A <see cref="ConnectorProviders">
        /// provider</see> for a broker with one transport, such as
        /// <c>ConnectorProviders.RabbitMq</c>, whose connection renames itself by deployment
        /// and so cannot be matched on its transport. A <see cref="ConnectorTransports">
        /// transport</see> wherever one provider covers several, such as
        /// <c>ConnectorTransports.AzureServiceBus</c>, since claiming <c>Azure</c> there would
        /// also claim Azure Queue Storage on no evidence.
        /// </para>
        ///
        /// <para>
        /// Add an entry only when a round-trip test covers it. The name says "providers" for
        /// historical reasons; it holds both.
        /// </para>
        /// </summary>
        public IReadOnlySet<string> VerifiedProviders { get; }

        public string CreateWrappedMessage(IMessageRequest request);
    }
}
