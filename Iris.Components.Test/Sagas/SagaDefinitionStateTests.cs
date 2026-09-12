using System.Runtime.Loader;
using FluentAssertions;
using Iris.Assemblies;
using Iris.Components.Sagas;
using Iris.Components.Sagas.Messages;
using Iris.Sagas;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;

namespace Iris.Components.Test.Sagas;

public class SagaDefinitionStateTests
{
    private static LoadedAssembly Assembly(System.Reflection.Assembly assembly)
        => new() { Assembly = assembly, Context = AssemblyLoadContext.Default };

    private static SagaGraph Graph(string typeName) => new(typeName, typeName.Split('.').Last(), "Stub",
        [new SagaState("Initial", true, false), new SagaState("Done", false, false)],
        [new SagaTransitionDefinition("Initial", "Done", "Go")]);

    private static (SagaDefinitionState State, IMessageBus Bus, ISagaDefinitionProvider Provider) Create()
    {
        var bus = Substitute.For<IMessageBus>();
        var provider = Substitute.For<ISagaDefinitionProvider>();
        provider.Framework.Returns("Stub");
        return (new SagaDefinitionState([provider], bus), bus, provider);
    }

    [Fact(DisplayName = "Adding an assembly runs every provider and exposes usable and failed results")]
    public async Task Add_Assembly_Collects_Results()
    {
        var (state, bus, provider) = Create();
        var loaded = Assembly(typeof(SagaDefinitionStateTests).Assembly);
        provider.Discover(loaded).Returns([
            new SagaDefinitionResult(Graph("A.Order"), "A.Order", null),
            new SagaDefinitionResult(null, "A.Broken", "No parameterless constructor"),
        ]);

        await state.AddAssemblyAsync(loaded);

        state.Definitions.Should().HaveCount(2);
        state.Graphs.Should().ContainSingle(g => g.TypeName == "A.Order");
        state.FindGraph("A.Order").Should().NotBeNull();
        state.FindGraph("A.Broken").Should().BeNull();
        await bus.Received(1).PublishAsync(Arg.Is<SagaDefinitionsChanged>(m => m.RemovedTypeNames.Count == 0));
    }

    [Fact(DisplayName = "Removing an assembly drops its definitions and reports the removed type names")]
    public async Task Remove_Assembly_Drops_Definitions()
    {
        var (state, bus, provider) = Create();
        var loaded = Assembly(typeof(SagaDefinitionStateTests).Assembly);
        provider.Discover(loaded).Returns([new SagaDefinitionResult(Graph("A.Order"), "A.Order", null)]);
        await state.AddAssemblyAsync(loaded);

        var removed = await state.RemoveAssemblyAsync(loaded.Assembly.FullName!);

        removed.Should().Equal("A.Order");
        state.Definitions.Should().BeEmpty();
        await bus.Received(1).PublishAsync(Arg.Is<SagaDefinitionsChanged>(m => m.RemovedTypeNames.SequenceEqual(new[] { "A.Order" })));
    }

    [Fact(DisplayName = "Removing an unknown assembly is a no-op that publishes nothing")]
    public async Task Remove_Unknown_Assembly_Is_Noop()
    {
        var (state, bus, _) = Create();

        var removed = await state.RemoveAssemblyAsync("Nope, Version=1.0.0.0");

        removed.Should().BeEmpty();
        await bus.DidNotReceive().PublishAsync(Arg.Any<SagaDefinitionsChanged>());
    }
}
