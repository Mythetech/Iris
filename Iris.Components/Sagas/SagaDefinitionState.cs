using Iris.Assemblies;
using Iris.Components.Sagas.Messages;
using Iris.Sagas;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Iris.Components.Sagas;

/// <summary>
/// Saga definitions reflected from every loaded assembly, keyed by assembly full name.
/// Fed by <see cref="Consumers.AssemblyChangeConsumer"/>; announces changes with <see cref="SagaDefinitionsChanged"/>.
/// </summary>
public sealed class SagaDefinitionState
{
    private readonly IReadOnlyList<ISagaDefinitionProvider> _providers;
    private readonly IMessageBus _bus;
    private readonly object _gate = new();
    private readonly Dictionary<string, List<SagaDefinitionResult>> _byAssembly = new(StringComparer.Ordinal);

    public SagaDefinitionState(IEnumerable<ISagaDefinitionProvider> providers, IMessageBus bus)
    {
        _providers = providers.ToList();
        _bus = bus;
    }

    public IReadOnlyList<SagaDefinitionResult> Definitions
    {
        get { lock (_gate) return _byAssembly.Values.SelectMany(v => v).ToList(); }
    }

    public IReadOnlyList<SagaGraph> Graphs => Definitions.Where(d => d.Graph is not null).Select(d => d.Graph!).ToList();

    public SagaGraph? FindGraph(string typeName) => Graphs.FirstOrDefault(g => g.TypeName == typeName);

    public async Task AddAssemblyAsync(LoadedAssembly assembly)
    {
        var key = assembly.Assembly.FullName ?? assembly.Assembly.GetName().Name ?? Guid.NewGuid().ToString("N");
        var results = _providers.SelectMany(p => p.Discover(assembly)).ToList();
        lock (_gate)
            _byAssembly[key] = results;
        await _bus.PublishAsync(new SagaDefinitionsChanged([]));
    }

    public async Task<IReadOnlyList<string>> RemoveAssemblyAsync(string fullName)
    {
        List<string> removed;
        lock (_gate)
        {
            if (!_byAssembly.Remove(fullName, out var results))
                return [];
            removed = results.Select(r => r.TypeName).ToList();
        }
        await _bus.PublishAsync(new SagaDefinitionsChanged(removed));
        return removed;
    }
}
