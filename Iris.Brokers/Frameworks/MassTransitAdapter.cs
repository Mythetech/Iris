using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Iris.Contracts.Messaging.Frameworks;
using MassTransit;

namespace Iris.Brokers.Frameworks
{
    public class MassTransitAdapter : IFramework
    {
        public string Name => "MassTransit";

        public IReadOnlyList<FrameworkKey> Keys { get; } =
        [
            FrameworkKey.Body("messageType", required: true),
            FrameworkKey.Body("messageId"),
            FrameworkKey.Body("correlationId"),
            FrameworkKey.Body("conversationId"),
            FrameworkKey.Body("sourceAddress"),
            FrameworkKey.Body("sentTime"),
            FrameworkKey.Body("host"),
        ];

        /// <summary>
        /// Three round trips back this: RabbitMQ, Amazon SQS and Azure Service Bus.
        ///
        /// <para>
        /// Azure Service Bus is named as a transport rather than as the Azure provider on
        /// purpose. Claiming the provider, which is what <c>ConnectorProviders.All</c> did
        /// here, also claimed Azure Queue Storage, because one connector reports
        /// <c>Azure</c> for both. There is no Queue Storage round trip for MassTransit, and
        /// the two transports do not share a wire format, so that half of the claim was
        /// never evidence-backed.
        /// </para>
        /// </summary>
        public IReadOnlySet<string> VerifiedProviders { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ConnectorProviders.RabbitMq,
                ConnectorProviders.Amazon,
                ConnectorTransports.AzureServiceBus,
            };

        public FrameworkDescriptor Descriptor { get; } = new("MassTransit",
        [
            CommonInputs.TypeName("the urn:message type"),
        ]);

        public string CreateWrappedMessage(IMessageRequest request)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Json);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.MessageType);

            string json = request.Json;
            string messageType = request.MessageFullyQualifiedName ?? request.MessageType;

            var envelope = new IrisMessageEnvelope(
                JsonSerializer.Deserialize<dynamic>(json ?? "") ?? "",
                ToMessageUrn(messageType!));

            return JsonSerializer.Serialize(envelope);
        }

        /// <summary>
        /// MassTransit identifies a message as <c>urn:message:{Namespace}:{TypeName}</c>, so the
        /// namespace keeps its dots but the separator before the type name is a colon. The value
        /// arriving here is spelled one of three ways:
        /// <list type="bullet">
        /// <item>an Azure Service Bus entity name, <c>MyApp.Messages/OrderPlaced</c>, because
        /// <c>ServiceBusMessageNameFormatter</c> uses <c>/</c> as its namespace separator</item>
        /// <item>a RabbitMQ exchange name, <c>MyApp.Messages:OrderPlaced</c>, which is already the
        /// urn spelling because <c>RabbitMqMessageNameFormatter</c> uses <c>:</c></item>
        /// <item>a .NET fully qualified name, <c>MyApp.Messages.OrderPlaced</c>, which is what the
        /// type picker and the Type name field's own help text produce</item>
        /// </list>
        /// Only the final dot is rewritten, so a bare endpoint name with no namespace is left
        /// exactly as it was given.
        /// </summary>
        private static string ToMessageUrn(string messageType)
        {
            var name = messageType.Replace('/', ':');

            if (!name.Contains(':'))
            {
                var lastDot = name.LastIndexOf('.');
                if (lastDot > 0 && lastDot < name.Length - 1)
                {
                    name = string.Concat(name.AsSpan(0, lastDot), ":", name.AsSpan(lastDot + 1));
                }
            }

            return $"urn:message:{name}";
        }
    }

    /// <summary>
    /// Every identifier and timestamp is minted once, in the constructor. These were previously
    /// expression-bodied properties calling <c>Guid.NewGuid()</c>, so a second read of the same
    /// envelope disagreed with the first and no two identifiers could ever be related.
    /// </summary>
    internal class IrisMessageEnvelope : MassTransit.Serialization.MessageEnvelope
    {
        /// <summary>
        /// MassTransit treats every address as an absolute endpoint URI. Iris is not a bus and
        /// has no receive endpoint, so it identifies itself on the loopback host the in-memory
        /// transport uses rather than sending a bare word a consumer cannot parse.
        /// </summary>
        private const string IrisSourceAddress = "loopback://localhost/iris";

        public IrisMessageEnvelope(object message, string messageUrn)
        {
            Message = message;
            MessageType = [messageUrn];
            MessageId = Guid.NewGuid().ToString();
            CorrelationId = Guid.NewGuid().ToString();
            ConversationId = Guid.NewGuid().ToString();
            SentTime = DateTime.UtcNow;
        }

        public string? MessageId { get; }

        /// <summary>
        /// Null on purpose. A non-null RequestId tells a consumer this is a request awaiting a
        /// response, which changes how it replies and where faults go.
        /// </summary>
        public string? RequestId => null;

        public string? CorrelationId { get; }

        public string? ConversationId { get; }

        /// <summary>
        /// Null on purpose. InitiatorId names the message that caused this one, and nothing
        /// caused a message a user typed into Iris.
        /// </summary>
        public string? InitiatorId => null;

        public string? SourceAddress => IrisSourceAddress;

        public string? DestinationAddress => null;

        public string? ResponseAddress => null;

        public string? FaultAddress => null;

        public string[]? MessageType { get; }

        public object? Message { get; }

        public DateTime? ExpirationTime => null;

        public DateTime? SentTime { get; }

        public Dictionary<string, object?>? Headers { get; } = new();

        public HostInfo? Host { get; } = new IrisHostInfo();
    }

    internal class IrisHostInfo : HostInfo
    {
        // Read once. GetCurrentProcess allocates a handle per call, and none of these values
        // change over the life of the process.
        private static readonly string ProcessNameValue = Process.GetCurrentProcess().ProcessName;

        private static readonly System.Reflection.AssemblyName? EntryAssembly =
            System.Reflection.Assembly.GetEntryAssembly()?.GetName();

        private static readonly string? MassTransitVersionValue =
            FileVersionInfo.GetVersionInfo(typeof(IBus).Assembly.Location).FileVersion;

        public string? MachineName => Environment.MachineName;

        public string? ProcessName => ProcessNameValue;

        public int ProcessId => Environment.ProcessId;

        public string? Assembly => EntryAssembly?.Name;

        public string? AssemblyVersion => EntryAssembly?.Version?.ToString() ?? "1.0.0.0";

        public string? FrameworkVersion => RuntimeInformation.FrameworkDescription;

        public string? MassTransitVersion => MassTransitVersionValue;

        public string? OperatingSystemVersion => Environment.OSVersion.VersionString;
    }
}
