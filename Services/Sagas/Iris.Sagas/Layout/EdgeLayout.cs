namespace Iris.Sagas.Layout;

public sealed record EdgeLayout(
    SagaTransitionDefinition Transition,
    string Path,
    double LabelX,
    double LabelY,
    bool IsBackEdge,
    bool IsSelfLoop);
