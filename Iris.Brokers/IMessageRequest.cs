using Iris.Brokers.Models;

namespace Iris.Brokers;

public interface IMessageRequest
{
    string MessageType { get; set; }

    string? MessageFullyQualifiedName { get; set; }

    string? MessageAssemblyName { get; set; }

    string Json { get; set; }

    string? Framework { get; set; }

    /// <summary>User-supplied framework inputs (TypeName, AssemblyName, Topic, ...).</summary>
    Dictionary<string, string> Properties { get; set; }

    /// <summary>Application headers. Carriers map these to AMQP headers, Service Bus application properties, SQS message attributes.</summary>
    Dictionary<string, string> Headers { get; set; }

    /// <summary>Native message properties. Carriers map these to AMQP basic properties, Service Bus system properties.</summary>
    TransportProperties TransportProperties { get; set; }

    HeaderDataType HeaderTypeOf(string key);
}