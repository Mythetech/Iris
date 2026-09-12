using System.Runtime.Loader;
using FluentAssertions;
using Iris.Assemblies;
using Iris.Sagas.Frameworks;
using Iris.Sagas.Test.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Iris.Sagas.Test;

public class MassTransitSagaDefinitionProviderTests
{
    private static LoadedAssembly TestAssembly() => new()
    {
        Assembly = typeof(OrderTestStateMachine).Assembly,
        Context = AssemblyLoadContext.Default,
    };

    private static IReadOnlyList<SagaDefinitionResult> Discover()
        => new MassTransitSagaDefinitionProvider().Discover(TestAssembly());

    [Fact(DisplayName = "Finds every MassTransitStateMachine subclass in the assembly")]
    public void Finds_All_State_Machines()
    {
        var results = Discover();

        results.Select(r => r.TypeName).Should().BeEquivalentTo(
            typeof(OrderTestStateMachine).FullName,
            typeof(CancellingOrderTestStateMachine).FullName,
            typeof(FinalizingTestStateMachine).FullName,
            typeof(DependentTestStateMachine).FullName,
            typeof(ThrowingTestStateMachine).FullName,
            typeof(StaticThrowingTestStateMachine).FullName);
    }

    [Fact(DisplayName = "Graph display name is the simple class name MassTransit stamps on spans")]
    public void Display_Name_Is_Simple_Class_Name()
    {
        var order = Discover().Single(r => r.TypeName == typeof(OrderTestStateMachine).FullName);

        order.Graph!.DisplayName.Should().Be("OrderTestStateMachine");
        order.Graph.Framework.Should().Be("MassTransit");
    }

    [Fact(DisplayName = "States include Initial flagged as initial and every declared state")]
    public void Maps_States()
    {
        var graph = Discover().Single(r => r.TypeName == typeof(OrderTestStateMachine).FullName).Graph!;

        graph.States.Select(s => s.Name).Should().BeEquivalentTo("Initial", "Submitted", "Accepted", "Shipped", "Cancelled", "Failed");
        graph.States.Single(s => s.Name == "Initial").IsInitial.Should().BeTrue();
        graph.States.Where(s => s.Name != "Initial").Should().OnlyContain(s => !s.IsInitial && !s.IsFinal);
    }

    [Fact(DisplayName = "Transitions pair each state with its own event targets, including a shared event")]
    public void Maps_Transitions_Without_Cross_Pairing()
    {
        var graph = Discover().Single(r => r.TypeName == typeof(OrderTestStateMachine).FullName).Graph!;

        graph.Transitions.Should().BeEquivalentTo(new[]
        {
            new SagaTransitionDefinition("Initial", "Submitted", "Submit"),
            new SagaTransitionDefinition("Submitted", "Accepted", "Accept"),
            new SagaTransitionDefinition("Submitted", "Cancelled", "Cancel"),
            new SagaTransitionDefinition("Accepted", "Shipped", "Ship"),
            new SagaTransitionDefinition("Accepted", "Cancelled", "Cancel"),
            new SagaTransitionDefinition("Shipped", "Failed", "Cancel"),
        });
    }

    [Fact(DisplayName = "A shared event never invents a transition to another source state's target")]
    public void Does_Not_Invent_Transitions_For_A_Shared_Event()
    {
        var graph = Discover().Single(r => r.TypeName == typeof(OrderTestStateMachine).FullName).Graph!;

        graph.Transitions.Should().NotContain(new SagaTransitionDefinition("Accepted", "Failed", "Cancel"));
        graph.Transitions.Should().NotContain(new SagaTransitionDefinition("Submitted", "Failed", "Cancel"));
        graph.Transitions.Should().NotContain(new SagaTransitionDefinition("Shipped", "Cancelled", "Cancel"));
    }

    [Fact(DisplayName = "Finalize produces a transition into Final flagged as final")]
    public void Maps_Final_State()
    {
        var graph = Discover().Single(r => r.TypeName == typeof(FinalizingTestStateMachine).FullName).Graph!;

        graph.States.Should().Contain(s => s.Name == "Final" && s.IsFinal);
        graph.Transitions.Should().ContainSingle().Which.Should().Be(new SagaTransitionDefinition("Initial", "Final", "Start"));
    }

    [Fact(DisplayName = "A state machine without a parameterless constructor is reported with the parameters it needs")]
    public void Reports_Unconstructible_Machine()
    {
        var dependent = Discover().Single(r => r.TypeName == typeof(DependentTestStateMachine).FullName);

        dependent.Graph.Should().BeNull();
        dependent.IsUsable.Should().BeFalse();
        dependent.Error.Should().Contain("IPricing").And.Contain("Int32");
    }

    [Fact(DisplayName = "A state machine whose constructor throws is reported with the underlying cause")]
    public void Reports_Throwing_Constructor()
    {
        var throwing = Discover().Single(r => r.TypeName == typeof(ThrowingTestStateMachine).FullName);

        throwing.IsUsable.Should().BeFalse();
        throwing.Error.Should().Be("Constructor threw: no broker configured");
    }

    [Fact(DisplayName = "A failing static constructor is reported by its cause, not by the type initializer wrapper")]
    public void Reports_Throwing_Static_Constructor()
    {
        var throwing = Discover().Single(r => r.TypeName == typeof(StaticThrowingTestStateMachine).FullName);

        throwing.IsUsable.Should().BeFalse();
        throwing.Error.Should().Be("Constructor threw: static setup failed");
    }

    [Fact(DisplayName = "An assembly with no state machines yields an empty list")]
    public void Empty_For_Plain_Assembly()
    {
        var plain = new LoadedAssembly { Assembly = typeof(SagaGraph).Assembly, Context = AssemblyLoadContext.Default };

        new MassTransitSagaDefinitionProvider().Discover(plain).Should().BeEmpty();
    }

    [Fact(DisplayName = "AddSagaServices registers the MassTransit provider and span mapper")]
    public void Registers_Saga_Services()
    {
        using var provider = new ServiceCollection().AddSagaServices().BuildServiceProvider();

        provider.GetRequiredService<ISagaDefinitionProvider>().Should().BeOfType<MassTransitSagaDefinitionProvider>();
        provider.GetRequiredService<ISagaSpanMapper>().Should().BeOfType<MassTransitSpanMapper>();
    }
}
