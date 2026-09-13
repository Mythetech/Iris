using Iris.Brokers.Models;

namespace Iris.Brokers;

/// <summary>
/// Marker base for send capabilities beyond the body-only guarantee of
/// <see cref="IConnection.SendAsync"/>. Mirrors <see cref="IMessageReader"/>:
/// a connection implements only the carriers it honours, and callers pattern-match.
/// The members are real declarations of what the transport can map, so the
/// compatibility check can reason about limits instead of a bare flag.
/// </summary>
public interface IMessageSender { }

/// <summary>Maps <see cref="IMessageRequest.Headers"/> onto the transport's application headers.</summary>
public interface IHeaderCarrier : IMessageSender
{
    /// <summary>Upper bound on the number of headers a single message may carry.</summary>
    int MaxHeaderCount { get; }

    bool IsValidHeaderKey(string key);

    /// <summary>Header value types the transport can encode natively.</summary>
    IReadOnlySet<HeaderDataType> SupportedDataTypes { get; }
}

/// <summary>Maps <see cref="IMessageRequest.TransportProperties"/> onto the transport's native message properties.</summary>
public interface ITransportPropertyCarrier : IMessageSender
{
    IReadOnlySet<TransportProperty> SupportedProperties { get; }
}
