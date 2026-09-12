using System.Reflection;
using Iris.Assemblies;
using MassTransit;
using MassTransit.SagaStateMachine;

namespace Iris.Sagas.Frameworks;

/// <summary>
/// Instantiates every <see cref="MassTransitStateMachine{TInstance}"/> in a loaded assembly and converts
/// it into the framework neutral <see cref="SagaGraph"/>.
/// </summary>
public sealed class MassTransitSagaDefinitionProvider : ISagaDefinitionProvider
{
    private const BindingFlags AnyConstructor = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public string Framework => "MassTransit";

    public IReadOnlyList<SagaDefinitionResult> Discover(LoadedAssembly assembly)
    {
        return LoadTypes(assembly.Assembly)
            .Where(IsStateMachine)
            .Select(Describe)
            .ToList();
    }

    private static IEnumerable<Type> LoadTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    private static bool IsStateMachine(Type type)
        => type is { IsClass: true, IsAbstract: false } && FindInstanceType(type) is not null;

    private static Type? FindInstanceType(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(MassTransitStateMachine<>))
                return current.GetGenericArguments()[0];
        }

        return null;
    }

    // Every step here is reflection over a type Iris did not compile against, including the signature
    // binding inside GetConstructor, so any of it can throw when the user's state machine references an
    // assembly that did not travel with the DLL they loaded. Discover materialises the whole sequence, so
    // an escaping exception would cost the user every other state machine in the assembly, not just this one.
    private SagaDefinitionResult Describe(Type type)
    {
        var typeName = type.FullName ?? type.Name;

        try
        {
            return DescribeStateMachine(type, typeName);
        }
        catch (Exception ex)
        {
            return new SagaDefinitionResult(null, typeName, $"Could not inspect this state machine: {RootCause(ex)}");
        }
    }

    private SagaDefinitionResult DescribeStateMachine(Type type, string typeName)
    {
        if (type.GetConstructor(Type.EmptyTypes) is null)
            return new SagaDefinitionResult(null, typeName, DescribeMissingConstructor(type));

        StateMachine machine;
        try
        {
            machine = (StateMachine)Activator.CreateInstance(type)!;
        }
        catch (Exception ex)
        {
            return new SagaDefinitionResult(null, typeName, $"Constructor threw: {RootCause(ex)}");
        }

        var visitor = new MassTransitTransitionVisitor();
        machine.Accept(visitor);

        var states = MapStates(machine, GetGraph(machine, FindInstanceType(type)!));
        var transitions = visitor.Transitions.Distinct().ToList();

        return new SagaDefinitionResult(
            new SagaGraph(typeName, machine.Name, Framework, states, transitions),
            typeName,
            null);
    }

    private static string DescribeMissingConstructor(Type type)
    {
        var parameters = type.GetConstructors(AnyConstructor).FirstOrDefault()?.GetParameters() ?? [];

        return parameters.Length == 0
            ? "No public parameterless constructor, so Iris cannot create this state machine."
            : $"No parameterless constructor. Iris cannot supply: "
              + string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
    }

    // MassTransit prunes its graph down to the states a transition actually reaches, which is the set worth
    // drawing, so the states come from there rather than from the machine's full state list.
    private static List<SagaState> MapStates(StateMachine machine, StateMachineGraph graph)
    {
        var initialName = machine.Initial.Name;
        var finalName = machine.Final.Name;

        return graph.Vertices
            .Where(v => v.VertexType == typeof(State))
            .Select(v => new SagaState(v.Title, v.Title == initialName, v.Title == finalName))
            .ToList();
    }

    private static StateMachineGraph GetGraph(StateMachine machine, Type instanceType)
    {
        var getGraph = typeof(GraphStateMachineExtensions)
            .GetMethod(nameof(GraphStateMachineExtensions.GetGraph))!
            .MakeGenericMethod(instanceType);

        return (StateMachineGraph)getGraph.Invoke(null, [machine])!;
    }

    // Reflection and static initialisation both bury the useful message under a wrapper whose own message
    // says nothing more than that something inside it failed.
    private static string RootCause(Exception exception)
    {
        var current = exception;
        while (current is TargetInvocationException or TypeInitializationException && current.InnerException is { } inner)
            current = inner;

        return current.Message;
    }
}
