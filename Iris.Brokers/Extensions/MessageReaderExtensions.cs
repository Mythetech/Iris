namespace Iris.Brokers.Extensions;

/// <summary>
/// Capability probes for <see cref="IConnection"/>. Each probe is a
/// thin type-check against the operation-specific interface; there are
/// no runtime capability flags to query.
/// </summary>
public static class MessageReaderExtensions
{
    /// <summary>True if the connection implements any read interface at all.</summary>
    public static bool SupportsRead(this IConnection connection)
        => connection is IMessageReader;

    /// <summary>True if the connection can non-destructively peek the main queue.</summary>
    public static bool SupportsPeek(this IConnection connection)
        => connection is IMessagePeeker;

    /// <summary>True if the connection can destructively receive from the main queue.</summary>
    public static bool SupportsReceive(this IConnection connection)
        => connection is IMessageReceiver;

    /// <summary>True if the connection can non-destructively peek the dead-letter sub-queue.</summary>
    public static bool SupportsDeadLetterPeek(this IConnection connection)
        => connection is IDeadLetterPeeker;

    /// <summary>True if the connection can destructively receive from the dead-letter sub-queue.</summary>
    public static bool SupportsDeadLetterReceive(this IConnection connection)
        => connection is IDeadLetterReceiver;
}
