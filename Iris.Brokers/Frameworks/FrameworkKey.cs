using Iris.Brokers.Models;

namespace Iris.Brokers.Frameworks;

public enum KeyLocation
{
    Body,
    Header,
    TransportProperty,
}

/// <summary>
/// One thing an adapter writes: where it lands, how it is typed, and whether the
/// consuming framework can do without it. Required means the consumer cannot
/// deserialize or route the message without it, as read from that framework's
/// receive pipeline. Body keys describe envelope fields for display only.
/// </summary>
public sealed record FrameworkKey(
    string Name,
    KeyLocation Location,
    HeaderDataType DataType = HeaderDataType.String,
    bool IsRequired = false,
    TransportProperty? Property = null)
{
    public static FrameworkKey Body(string name, bool required = false)
        => new(name, KeyLocation.Body, IsRequired: required);

    public static FrameworkKey Header(string name, HeaderDataType type = HeaderDataType.String, bool required = false)
        => new(name, KeyLocation.Header, type, required);

    public static FrameworkKey Transport(TransportProperty property, bool required = false)
        => new(property.ToString(), KeyLocation.TransportProperty, IsRequired: required, Property: property);
}
