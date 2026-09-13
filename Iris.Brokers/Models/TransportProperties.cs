namespace Iris.Brokers.Models;

public enum TransportProperty
{
    MessageId,
    CorrelationId,
    ContentType,
    Type,
    Timestamp,
    Persistent,
}

public enum HeaderDataType
{
    String,
    Integer,
    Boolean,
    Timestamp,
}

/// <summary>
/// Native, transport-level message properties (AMQP basic properties, Service Bus
/// system properties). Kept apart from <see cref="IMessageRequest.Headers"/> so a
/// carrier can map each bag to the right place instead of guessing.
/// </summary>
public sealed class TransportProperties
{
    public string? MessageId { get; set; }

    public string? CorrelationId { get; set; }

    public string? ContentType { get; set; }

    public string? Type { get; set; }

    public DateTimeOffset? Timestamp { get; set; }

    public bool? Persistent { get; set; }

    public bool IsSet(TransportProperty property) => property switch
    {
        TransportProperty.MessageId => MessageId is not null,
        TransportProperty.CorrelationId => CorrelationId is not null,
        TransportProperty.ContentType => ContentType is not null,
        TransportProperty.Type => Type is not null,
        TransportProperty.Timestamp => Timestamp is not null,
        TransportProperty.Persistent => Persistent is not null,
        _ => false,
    };

    public void Clear(TransportProperty property)
    {
        switch (property)
        {
            case TransportProperty.MessageId: MessageId = null; break;
            case TransportProperty.CorrelationId: CorrelationId = null; break;
            case TransportProperty.ContentType: ContentType = null; break;
            case TransportProperty.Type: Type = null; break;
            case TransportProperty.Timestamp: Timestamp = null; break;
            case TransportProperty.Persistent: Persistent = null; break;
        }
    }

    public IReadOnlyList<TransportProperty> SetProperties()
        => Enum.GetValues<TransportProperty>().Where(IsSet).ToList();
}
