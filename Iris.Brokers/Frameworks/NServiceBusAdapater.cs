using System.Text;
using System.Text.Json;
using NServiceBus.Transport;

namespace Iris.Brokers.Frameworks
{
    public class NServiceBusAdapater : IFramework
    {
        public string Name => "NServiceBus";

        public string CreateWrappedMessage(IMessageRequest apiMessageRequest)
        {
            var json = apiMessageRequest.Json;
            var messageType = apiMessageRequest.MessageFullyQualifiedName ?? apiMessageRequest.MessageType;

            ArgumentException.ThrowIfNullOrWhiteSpace(json);
            ArgumentException.ThrowIfNullOrWhiteSpace(messageType);

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
            { "NServiceBus.EnclosedMessageTypes", messageType },
            { "NServiceBus.Version", "9.1.0" },
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

