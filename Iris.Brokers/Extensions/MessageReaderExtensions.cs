namespace Iris.Brokers.Extensions;

/// <summary>
/// Capability probes and helpers for treating an <see cref="IConnection"/>
/// as an <see cref="IMessageReader"/>.
/// </summary>
public static class MessageReaderExtensions
{
    /// <summary>True if the connection implements <see cref="IMessageReader"/> at all.</summary>
    public static bool SupportsRead(this IConnection connection)
        => connection is IMessageReader;

    /// <summary>True if the connection can non-destructively peek messages.</summary>
    public static bool SupportsPeek(this IConnection connection)
        => connection is IMessageReader reader && reader.Capabilities.SupportsPeek;

    /// <summary>True if the connection can destructively receive messages.</summary>
    public static bool SupportsReceive(this IConnection connection)
        => connection is IMessageReader reader && reader.Capabilities.SupportsReceive;

    /// <summary>True if the connection exposes a dead-letter sub-queue.</summary>
    public static bool SupportsDeadLetter(this IConnection connection)
        => connection is IMessageReader reader && reader.Capabilities.SupportsDeadLetter;

    /// <summary>
    /// Get the reader view of a connection, throwing a friendly exception
    /// if the broker doesn't support reading.
    /// </summary>
    public static IMessageReader AsReader(this IConnection connection)
        => connection as IMessageReader
            ?? throw new NotSupportedException(
                $"Connection '{connection.Name}' ({connection.Address}) does not support reading.");
}
