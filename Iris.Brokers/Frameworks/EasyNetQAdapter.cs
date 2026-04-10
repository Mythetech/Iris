namespace Iris.Brokers.Frameworks;

public class EasyNetQAdapter : IFramework
{
    public string Name => "EasyNetQ";

    public string CreateWrappedMessage(IMessageRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Json);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MessageType);

        var typeName = BuildEasyNetQTypeName(request);

        var messageId = Guid.NewGuid().ToString();

        // EasyNetQ readers look at AMQP basic properties, not the body.
        // RabbitMqConnection.SendAsync forwards IMessageRequest.Headers
        // straight into PublishInfo.Properties (AMQP basic props), so we
        // mutate the headers dict here to set the AMQP properties EasyNetQ
        // consumers expect.
        request.Headers["type"] = typeName;
        request.Headers["content_type"] = "application/json";
        request.Headers["message_id"] = messageId;
        request.Headers["correlation_id"] = request.Headers.TryGetValue("correlation_id", out var cid)
            ? cid
            : messageId;
        request.Headers["delivery_mode"] = "2"; // persistent
        request.Headers["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        // EasyNetQ body is the raw POCO JSON — no wrapping envelope.
        return request.Json;
    }

    // EasyNetQ DefaultTypeNameSerializer format: "{FullName}:{AssemblyName}".
    private static string BuildEasyNetQTypeName(IMessageRequest request)
    {
        var fqn = request.MessageFullyQualifiedName ?? request.MessageType;

        // Preferred path: real assembly name threaded through from the loader.
        if (!string.IsNullOrWhiteSpace(request.MessageAssemblyName))
            return $"{fqn}:{request.MessageAssemblyName}";

        // Back-compat: caller stuffed an assembly-qualified name into the FQN field.
        var commaIdx = fqn.IndexOf(',');
        if (commaIdx > 0)
        {
            var fullName = fqn[..commaIdx].Trim();
            var asmPart = fqn[(commaIdx + 1)..].Trim();
            var asmName = asmPart.Split(',', 2)[0].Trim();
            return $"{fullName}:{asmName}";
        }

        // Last-resort fallback. Consumers must hand-register this mapping.
        return $"{fqn}:Messages";
    }
}
