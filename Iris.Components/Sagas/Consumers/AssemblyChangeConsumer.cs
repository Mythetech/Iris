using Iris.Assemblies.Messages;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Iris.Components.Sagas.Consumers;

public sealed class AssemblyChangeConsumer : IConsumer<AssemblyLoaded>, IConsumer<AssemblyUnloaded>
{
    private readonly SagaDefinitionState _definitions;
    private readonly SagaInstanceState _instances;

    public AssemblyChangeConsumer(SagaDefinitionState definitions, SagaInstanceState instances)
    {
        _definitions = definitions;
        _instances = instances;
    }

    public Task Consume(AssemblyLoaded message) => _definitions.AddAssemblyAsync(message.Assembly);

    public async Task Consume(AssemblyUnloaded message)
    {
        var removed = await _definitions.RemoveAssemblyAsync(message.FullName);
        if (removed.Count > 0)
            await _instances.RemoveGraphsAsync(removed);
    }
}
