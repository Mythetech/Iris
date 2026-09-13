namespace Iris.Contracts.Messaging.Frameworks;

/// <summary>One user-supplied value a framework adapter reads from the message's properties.</summary>
public sealed record FrameworkInput(
    string Key,
    string Label,
    string Description,
    bool Required = false,
    string? DefaultValue = null,
    IReadOnlyList<string>? AllowedValues = null);
