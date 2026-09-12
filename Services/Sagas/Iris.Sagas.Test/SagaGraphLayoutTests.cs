using FluentAssertions;
using Iris.Sagas;
using Iris.Sagas.Layout;

namespace Iris.Sagas.Test;

public class SagaGraphLayoutTests
{
    private static SagaGraph Graph(IEnumerable<string> states, params SagaTransitionDefinition[] transitions)
        => new("T", "T", "MassTransit",
            states.Select(s => new SagaState(s, s == "Initial", s == "Final")).ToList(),
            transitions);

    private static SagaGraph OrderGraph() => Graph(
        ["Initial", "Submitted", "Accepted", "Shipped", "Cancelled"],
        new("Initial", "Submitted", "Submit"),
        new("Submitted", "Accepted", "Accept"),
        new("Submitted", "Cancelled", "Cancel"),
        new("Accepted", "Shipped", "Ship"),
        new("Accepted", "Cancelled", "Cancel"));

    private static NodeLayout Node(SagaGraphLayoutResult result, string state) => result.Nodes.Single(n => n.State == state);

    [Fact(DisplayName = "Layers follow the longest path from Initial")]
    public void Layers_By_Longest_Path()
    {
        var result = SagaGraphLayout.Compute(OrderGraph());

        Node(result, "Initial").Y.Should().BeLessThan(Node(result, "Submitted").Y);
        Node(result, "Submitted").Y.Should().BeLessThan(Node(result, "Accepted").Y);
        Node(result, "Accepted").Y.Should().BeLessThan(Node(result, "Shipped").Y);
        Node(result, "Cancelled").Y.Should().Be(Node(result, "Shipped").Y, "Cancelled is reachable from Accepted so it shares the deepest layer");
    }

    [Fact(DisplayName = "No two nodes overlap")]
    public void Nodes_Do_Not_Overlap()
    {
        var result = SagaGraphLayout.Compute(OrderGraph());

        foreach (var a in result.Nodes)
        foreach (var b in result.Nodes.Where(n => n != a))
        {
            var separated = a.Right <= b.X || b.Right <= a.X || a.Bottom <= b.Y || b.Bottom <= a.Y;
            separated.Should().BeTrue($"{a.State} and {b.State} overlap");
        }
    }

    [Fact(DisplayName = "Back edges are flagged and do not push their target deeper")]
    public void Back_Edges_Do_Not_Affect_Layering()
    {
        var graph = Graph(["Initial", "A", "B"],
            new("Initial", "A", "Start"),
            new("A", "B", "Next"),
            new("B", "A", "Retry"));

        var result = SagaGraphLayout.Compute(graph);

        Node(result, "A").Y.Should().BeLessThan(Node(result, "B").Y);
        result.Edges.Single(e => e.Transition.EventName == "Retry").IsBackEdge.Should().BeTrue();
        result.Edges.Single(e => e.Transition.EventName == "Next").IsBackEdge.Should().BeFalse();
    }

    [Fact(DisplayName = "Self loops are flagged")]
    public void Self_Loops_Flagged()
    {
        var graph = Graph(["Initial", "A"],
            new("Initial", "A", "Start"),
            new("A", "A", "Ping"));

        var result = SagaGraphLayout.Compute(graph);

        var loop = result.Edges.Single(e => e.Transition.EventName == "Ping");
        loop.IsSelfLoop.Should().BeTrue();
        loop.Path.Should().StartWith("M");
    }

    [Fact(DisplayName = "Unreachable states are placed in a bottom layer rather than dropped")]
    public void Unreachable_States_Kept()
    {
        var graph = Graph(["Initial", "A", "Orphan"], new SagaTransitionDefinition("Initial", "A", "Start"));

        var result = SagaGraphLayout.Compute(graph);

        Node(result, "Orphan").Y.Should().BeGreaterThan(Node(result, "A").Y);
    }

    [Fact(DisplayName = "Every edge has a path and a label position inside the canvas")]
    public void Edges_Have_Paths_And_Labels()
    {
        var result = SagaGraphLayout.Compute(OrderGraph());

        result.Edges.Should().HaveCount(5);
        result.Edges.Should().OnlyContain(e => e.Path.StartsWith("M") && e.Path.Contains("C"));
        result.Edges.Should().OnlyContain(e => e.LabelX >= 0 && e.LabelX <= result.Width && e.LabelY >= 0 && e.LabelY <= result.Height);
        result.Width.Should().BePositive();
        result.Height.Should().BePositive();
    }

    [Fact(DisplayName = "An empty graph yields an empty layout")]
    public void Empty_Graph()
    {
        var result = SagaGraphLayout.Compute(Graph([]));

        result.Nodes.Should().BeEmpty();
        result.Edges.Should().BeEmpty();
    }

    [Fact(DisplayName = "Duplicate transitions between the same states get distinct labels inside the canvas")]
    public void Duplicate_Transitions_Get_Distinct_Labels()
    {
        var transitions = Enumerable.Range(0, 10)
            .Select(i => new SagaTransitionDefinition("Initial", "A", $"Event{i}"))
            .ToArray();
        var graph = Graph(["Initial", "A"], transitions);

        var result = SagaGraphLayout.Compute(graph);

        var plainGridHeight = SagaGraphLayout.Padding * 2 + 2 * SagaGraphLayout.NodeHeight + SagaGraphLayout.LayerGap;

        result.Edges.Select(e => e.LabelY).Distinct().Should().HaveCount(10, "each duplicate transition needs its own label position");
        result.Edges.Should().OnlyContain(e => e.LabelX >= 0 && e.LabelX <= result.Width && e.LabelY >= 0 && e.LabelY <= result.Height);
        result.Height.Should().BeGreaterThan(plainGridHeight, "ten stacked labels push past the plain two-layer grid height");
    }
}
