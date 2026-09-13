using Iris.Brokers.Models;
using Iris.Contracts.Messaging.Frameworks;

namespace Iris.Brokers.Frameworks;

/// <summary>
/// EasyNetQ readers look at AMQP basic properties, not the body: the type name in
/// <c>type</c>, JSON in <c>content_type</c>. Those are transport properties here, so the
/// RabbitMQ carrier lifts them to the top level of the management API publish; a body
/// envelope or application headers would never be seen.
/// </summary>
public class EasyNetQAdapter : IFramework
{
    /// <summary>
    /// Key into <see cref="IMessageRequest.Properties"/> that selects the wire format for the
    /// AMQP <c>type</c> property. Value <c>"Legacy"</c> (case-insensitive) uses
    /// <c>LegacyTypeNameSerializer</c>'s colon form; anything else, including the key being
    /// absent, uses the EasyNetQ 8 default, <c>DefaultTypeNameSerializer</c>'s comma form.
    /// </summary>
    public const string TypeNameFormatKey = "EasyNetQTypeNameFormat";

    private const string LegacyFormatValue = "Legacy";

    public string Name => "EasyNetQ";

    public IReadOnlyList<FrameworkKey> Keys { get; } =
    [
        FrameworkKey.Transport(TransportProperty.Type, required: true),
        FrameworkKey.Transport(TransportProperty.ContentType),
        FrameworkKey.Transport(TransportProperty.MessageId),
        FrameworkKey.Transport(TransportProperty.CorrelationId),
        FrameworkKey.Transport(TransportProperty.Timestamp),
        FrameworkKey.Transport(TransportProperty.Persistent),
    ];

    public IReadOnlySet<string> VerifiedProviders { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ConnectorProviders.RabbitMq };

    public FrameworkDescriptor Descriptor { get; } = new("EasyNetQ",
    [
        CommonInputs.TypeName("the AMQP type property as Namespace.Type, Assembly"),
        CommonInputs.AssemblyName("the Assembly part of the AMQP type property", required: true),
        new FrameworkInput(TypeNameFormatKey, "Type name format",
            "Default matches EasyNetQ 8's DefaultTypeNameSerializer (Namespace.Type, Assembly). Legacy matches EasyNetQ 7 (Namespace.Type:Assembly).",
            DefaultValue: "Default", AllowedValues: ["Default", "Legacy"]),
    ]);

    public string CreateWrappedMessage(IMessageRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Json);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MessageType);

        var messageId = Guid.NewGuid().ToString();
        var transport = request.TransportProperties;

        transport.Type = BuildEasyNetQTypeName(request);
        transport.ContentType = "application/json";
        transport.MessageId = messageId;
        transport.CorrelationId ??= messageId;
        transport.Persistent = true;
        transport.Timestamp = DateTimeOffset.UtcNow;

        return request.Json;
    }

    // EasyNetQ 8's default ITypeNameSerializer, DefaultTypeNameSerializer, formats a type as
    // "{FullName}, {AssemblyName}" (comma-space). LegacyTypeNameSerializer, EasyNetQ 7's
    // format, uses "{FullName}:{AssemblyName}" (colon) instead; opt into it via
    // Properties[TypeNameFormatKey] = "Legacy" when interoperating with a v7 consumer.
    private static string BuildEasyNetQTypeName(IMessageRequest request)
    {
        var fqn = request.MessageFullyQualifiedName ?? request.MessageType;
        var useLegacyFormat = request.Properties.TryGetValue(TypeNameFormatKey, out var format)
            && string.Equals(format, LegacyFormatValue, StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(request.MessageAssemblyName))
            return useLegacyFormat
                ? $"{fqn}:{request.MessageAssemblyName}"
                : $"{fqn}, {request.MessageAssemblyName}";

        // Back-compat: caller stuffed an assembly-qualified name into the FQN field.
        var commaIdx = fqn.IndexOf(',');
        if (commaIdx > 0)
        {
            var fullName = fqn[..commaIdx].Trim();
            var asmPart = fqn[(commaIdx + 1)..].Trim();
            var asmName = asmPart.Split(',', 2)[0].Trim();
            return useLegacyFormat
                ? $"{fullName}:{asmName}"
                : $"{fullName}, {asmName}";
        }

        throw new ArgumentException(
            "EasyNetQ resolves message types as 'Namespace.Type, Assembly'; supply an assembly name.",
            nameof(request));
    }
}
