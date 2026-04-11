namespace Iris.Contracts.Brokers.Models;

/// <summary>
/// UI-facing projection of a connection's read capabilities, built by
/// the service layer by probing which <c>IMessage*</c> interfaces the
/// broker connection implements. Exposed so UI projects can shape the
/// read dialog (e.g. hide the Peek button for SQS) without taking a
/// dependency on <c>Iris.Brokers</c>.
/// </summary>
public sealed record ReaderCapabilitiesDto(
    bool CanPeek,
    bool CanReceive,
    bool CanPeekDeadLetter,
    bool CanReceiveDeadLetter,
    int MaxPeekBatchSize,
    int MaxReceiveBatchSize);
