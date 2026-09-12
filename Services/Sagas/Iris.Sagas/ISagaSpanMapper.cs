using System.Diagnostics.CodeAnalysis;
using Iris.Telemetry;

namespace Iris.Sagas;

public interface ISagaSpanMapper
{
    string Framework { get; }
    bool TryMap(ReceivedSpan span, [MaybeNullWhen(false)] out SagaTransition transition);
}
