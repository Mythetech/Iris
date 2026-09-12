namespace Iris.Sagas;

/// <summary>
/// One state machine type found in an assembly. <see cref="Graph"/> is null when the type was
/// found but could not be turned into a graph; <see cref="Error"/> says why.
/// </summary>
public sealed record SagaDefinitionResult(SagaGraph? Graph, string TypeName, string? Error)
{
    public bool IsUsable => Graph is not null;
}
