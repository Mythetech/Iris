using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using MassTransit;

namespace Iris.Brokers.Frameworks
{
    public class MassTransitAdapter : IFramework
    {
        public string Name => "MassTransit";

        public string CreateWrappedMessage(IMessageRequest request)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Json);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.MessageType);

            string json = request.Json;
            string messageType = request.MessageFullyQualifiedName ?? request.MessageType;

            var envelope = new IrisMessageEnvelope(JsonSerializer.Deserialize<dynamic>(json ?? "") ?? "", messageType!);

            return JsonSerializer.Serialize(envelope);
        }
    }

    internal class IrisMessageEnvelope : MassTransit.Serialization.MessageEnvelope
    {
        private readonly string _messageType;

        public IrisMessageEnvelope(object message, string messageType)
        {
            Message = message;
            _messageType = messageType!;
        }

        public string? MessageId => Guid.NewGuid().ToString();

        public string? RequestId => Guid.NewGuid().ToString();

        public string? CorrelationId => Guid.NewGuid().ToString();

        public string? ConversationId => Guid.NewGuid().ToString();

        public string? InitiatorId => Guid.NewGuid().ToString();

        public string? SourceAddress => "iris";

        public string? DestinationAddress => "";

        public string? ResponseAddress => default!;

        public string? FaultAddress => default!;

        public string[]? MessageType => [$"urn:message:{_messageType.Replace("/", ":")}"];

        public object? Message { get; }

        public DateTime? ExpirationTime => default!;

        public DateTime? SentTime => DateTime.Now;

        public Dictionary<string, object?>? Headers => new();

        public HostInfo? Host => new IrisHostInfo();
    }

    internal class IrisHostInfo : HostInfo
    {
        public string? MachineName => Environment.MachineName;

        public string? ProcessName => Process.GetCurrentProcess().ProcessName;

        public int ProcessId => Environment.ProcessId;

        public string? Assembly => System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;

        public string? AssemblyVersion => System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString() ?? "1.0.0.0";

        public string? FrameworkVersion => RuntimeInformation.FrameworkDescription;

        public string? MassTransitVersion => FileVersionInfo.GetVersionInfo(typeof(IBus).Assembly.Location).FileVersion;

        public string? OperatingSystemVersion => Environment.OSVersion.VersionString;
    }
}

