namespace Iris.Brokers.Models;

/// <summary>
/// Honest, per-broker capability flags for the read side. UI and service
/// layers gate behavior on these; unsupported operations also throw
/// <see cref="NotSupportedException"/> from the implementation.
/// </summary>
public sealed record ReaderCapabilities(
    bool SupportsPeek,
    bool SupportsBatchPeek,
    bool SupportsReceive,
    bool SupportsBatchReceive,
    bool SupportsDeadLetter,
    int MaxBatchSize);
