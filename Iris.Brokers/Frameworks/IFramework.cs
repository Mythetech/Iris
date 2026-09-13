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
        /// Connector provider names this adapter has a consumer round-trip test against. The
        /// key list describes one wire shape, read from the framework's RabbitMQ receive
        /// pipeline; other transports may spell the same metadata differently, so a provider
        /// that is absent here still sends but is flagged as unverified in the UI.
        /// </summary>
        public IReadOnlySet<string> VerifiedProviders { get; }

        public string CreateWrappedMessage(IMessageRequest request);
    }
}
