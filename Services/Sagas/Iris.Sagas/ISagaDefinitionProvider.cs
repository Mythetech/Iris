using Iris.Assemblies;

namespace Iris.Sagas;

public interface ISagaDefinitionProvider
{
    string Framework { get; }
    IReadOnlyList<SagaDefinitionResult> Discover(LoadedAssembly assembly);
}
