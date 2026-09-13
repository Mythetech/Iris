namespace Iris.Contracts.Messaging.Frameworks;

/// <summary>
/// UI-facing projection of a framework adapter: its name, the inputs it wants, and,
/// once matched against a provider, whether that provider can carry it and whether the
/// pairing has been proven against a real consumer. Lives in Contracts so UI projects
/// never reference Iris.Brokers.
/// </summary>
public sealed record FrameworkDescriptor(
    string Name,
    IReadOnlyList<FrameworkInput> Inputs,
    bool Supported = true,
    string? UnsupportedReason = null,
    IReadOnlyList<string>? DroppedKeys = null,
    bool Verified = true);
