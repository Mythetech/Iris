using System.Runtime.Loader;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using Iris.Assemblies;
using Iris.Sagas.Frameworks;
using Iris.Sagas.Test.Fixtures;

namespace Iris.Sagas.Test;

/// <summary>
/// The whole graph discovered from a state machine, as one committed fixture.
///
/// <para>
/// <see cref="MassTransitSagaDefinitionProviderTests"/> asserts state names, transition
/// counts and flags individually, each with the reason it matters. This pins the assembled
/// result: a transition that quietly disappears because MassTransit renamed an internal
/// activity type is a hole in the diagram the user sees, and an assertion that only counts
/// names would not notice which edge went missing.
/// </para>
/// </summary>
public class SagaGraphSnapshotTests
{
    [Fact(DisplayName = "The discovered graph for the order state machine matches the committed snapshot")]
    public void Order_graph_matches_the_fixture()
    {
        var assembly = new LoadedAssembly
        {
            Assembly = typeof(OrderTestStateMachine).Assembly,
            Context = AssemblyLoadContext.Default,
        };

        var graph = new MassTransitSagaDefinitionProvider()
            .Discover(assembly)
            .Single(r => r.TypeName == typeof(OrderTestStateMachine).FullName)
            .Graph!;

        var actual = JsonSerializer.Serialize(new
        {
            graph.DisplayName,
            graph.Framework,
            // Sorted: discovery order follows reflection, which is not a contract, while the
            // set of states and edges is.
            States = graph.States.OrderBy(s => s.Name, StringComparer.Ordinal),
            Transitions = graph.Transitions
                .OrderBy(t => t.FromState, StringComparer.Ordinal)
                .ThenBy(t => t.EventName, StringComparer.Ordinal)
                .ThenBy(t => t.ToState, StringComparer.Ordinal),
        }, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        var expectedPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "order-saga-graph.expected.json");
        var expected = File.ReadAllText(expectedPath);

        // TypeName is deliberately out of the fixture: it is the test assembly's own name,
        // which changes with the project, and the provider test already asserts it.
        Normalize(actual).Should().Be(Normalize(expected));
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").TrimEnd();
}
