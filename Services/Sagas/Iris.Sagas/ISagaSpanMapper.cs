using Iris.Telemetry;

namespace Iris.Sagas;

public interface ISagaSpanMapper
{
    string Framework { get; }
    bool TryMap(ReceivedSpan span, out SagaTransition transition);
}
