using System.Text;
using System.Text.Json;
using Iris.Contracts.Messaging.Frameworks;
using NServiceBus.Transport;

namespace Iris.Brokers.Frameworks
{
    public class NServiceBusAdapter : IFramework
    {
        private static readonly string NServiceBusVersion =
            typeof(OutgoingMessage).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        public string Name => "NServiceBus";

        public IReadOnlyList<FrameworkKey> Keys { get; } =
        [
            FrameworkKey.Body("Id", required: true),
            FrameworkKey.Body("Headers", required: true),
            FrameworkKey.Body("Body", required: true),
            FrameworkKey.Body("CorrelationId"),
            FrameworkKey.Body("MessageIntent"),
            FrameworkKey.Body("ReplyToAddress"),
        ];

        public IReadOnlySet<string> VerifiedProviders { get; } =
            new HashSet<string>(ConnectorProviders.All, StringComparer.OrdinalIgnoreCase);

        public FrameworkDescriptor Descriptor { get; } = new("NServiceBus",
        [
            CommonInputs.TypeName("NServiceBus.EnclosedMessageTypes"),
            CommonInputs.AssemblyName("the assembly-qualified EnclosedMessageTypes value"),
        ]);

        public string CreateWrappedMessage(IMessageRequest apiMessageRequest)
        {
            var json = apiMessageRequest.Json;
            var messageType = apiMessageRequest.MessageFullyQualifiedName ?? apiMessageRequest.MessageType;

            ArgumentException.ThrowIfNullOrWhiteSpace(json);
            ArgumentException.ThrowIfNullOrWhiteSpace(messageType);

            var enclosedMessageTypes = string.IsNullOrWhiteSpace(apiMessageRequest.MessageAssemblyName)
                ? messageType
                : $"{messageType}, {apiMessageRequest.MessageAssemblyName}";

            var messageId = Guid.NewGuid().ToString();
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false
            };
            var serializedJson = JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement, jsonOptions);
            var body = Encoding.UTF8.GetBytes(serializedJson);

            var headers = new Dictionary<string, string>
        {
            { "NServiceBus.MessageId", messageId },
            { "NServiceBus.MessageIntent", "Send" },
            { "NServiceBus.ConversationId", Guid.NewGuid().ToString() },
            { "NServiceBus.CorrelationId", messageId },
            { "NServiceBus.ReplyToAddress", messageType },
            { "NServiceBus.OriginatingMachine", Environment.MachineName },
            { "NServiceBus.OriginatingEndpoint", messageType },
            { "$.diagnostics.originating.hostid", Guid.NewGuid().ToString() },
            { "NServiceBus.ContentType", "application/json" },
            { "NServiceBus.EnclosedMessageTypes", enclosedMessageTypes },
            { "NServiceBus.Version", NServiceBusVersion },
            { "NServiceBus.TimeSent", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss:ffffff Z") }
        };

            var envelope = new OutgoingMessage(messageId, headers, body);

            return JsonSerializer.Serialize(new
            {
                IdForCorrelation = (string?)null,
                Id = envelope.MessageId,
                MessageIntent = 1,
                ReplyToAddress = messageType,
                TimeToBeReceived = "00:00:00",
                Headers = envelope.Headers,
                Body = Convert.ToBase64String(body),
                CorrelationId = envelope.Headers["NServiceBus.CorrelationId"],
                Recoverable = false
            });

        }
    }
}

