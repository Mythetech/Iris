namespace Iris.Sagas.Layout;

public sealed record SagaGraphLayoutResult(
    IReadOnlyList<NodeLayout> Nodes,
    IReadOnlyList<EdgeLayout> Edges,
    double Width,
    double Height);
