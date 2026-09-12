using System.Runtime.Loader;
using FluentAssertions;
using Iris.Assemblies;
using Iris.Sagas;
using Iris.Sagas.Frameworks;
using Iris.Sagas.Layout;
using Iris.Sagas.Test.Fixtures;

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

    // The sample's shape: CancelOrder is handled from two states and finalizes, so four
    // transitions carry the same event name and two of them leave the same state.
    private static SagaGraph FinalizingOrderGraph() => Graph(
        ["Initial", "Submitted", "Accepted", "Cancelled", "Final", "Shipped"],
        new("Initial", "Submitted", "OrderSubmitted"),
        new("Submitted", "Accepted", "OrderAccepted"),
        new("Submitted", "Cancelled", "OrderCancelled"),
        new("Submitted", "Final", "OrderCancelled"),
        new("Accepted", "Shipped", "OrderShipped"),
        new("Accepted", "Cancelled", "OrderCancelled"),
        new("Accepted", "Final", "OrderCancelled"));

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

    [Fact(DisplayName = "No edge label is placed on top of a node")]
    public void Edge_Labels_Clear_Node_Boxes()
    {
        var result = SagaGraphLayout.Compute(OrderGraph());

        foreach (var edge in result.Edges)
        {
            var box = SagaGraphLayout.LabelBox(edge);
            foreach (var node in result.Nodes)
            {
                box.Overlaps(node).Should().BeFalse(
                    $"the '{edge.Transition.EventName}' label on {edge.Transition.FromState} to {edge.Transition.ToState} "
                    + $"is drawn over the {node.State} node, which hides the text behind the node's fill");
            }
        }
    }

    // The hand built graphs above fix a state order, but the real one comes from MassTransit's own
    // vertex order, and the layout's x positions follow it. Laying out the genuinely extracted
    // graph is the only way to know the sample renders cleanly.
    [Fact(DisplayName = "The sample's own extracted graph lays out with nothing written over anything")]
    public void Extracted_Sample_Graph_Has_No_Overlaps()
    {
        var extracted = new MassTransitSagaDefinitionProvider()
            .Discover(new LoadedAssembly
            {
                Assembly = typeof(CancellingOrderTestStateMachine).Assembly,
                Context = AssemblyLoadContext.Default,
            })
            .Single(r => r.TypeName == typeof(CancellingOrderTestStateMachine).FullName);
        extracted.Graph.Should().NotBeNull(extracted.Error);

        var result = SagaGraphLayout.Compute(extracted.Graph!);

        var labels = result.Edges.Select(e => (e.Transition, Box: SagaGraphLayout.LabelBox(e))).ToList();
        foreach (var (transition, box) in labels)
        foreach (var node in result.Nodes)
            box.Overlaps(node).Should().BeFalse(
                $"'{transition.EventName}' ({transition.FromState} to {transition.ToState}) is drawn over the {node.State} node");

        for (var i = 0; i < labels.Count; i++)
        for (var j = i + 1; j < labels.Count; j++)
            labels[i].Box.Overlaps(labels[j].Box).Should().BeFalse(
                $"'{labels[i].Transition.EventName}' ({labels[i].Transition.FromState} to {labels[i].Transition.ToState}) and "
                + $"'{labels[j].Transition.EventName}' ({labels[j].Transition.FromState} to {labels[j].Transition.ToState}) are drawn over each other");
    }

    [Fact(DisplayName = "No two edge labels are drawn on top of each other")]
    public void Edge_Labels_Clear_Each_Other()
    {
        var result = SagaGraphLayout.Compute(FinalizingOrderGraph());

        var labels = result.Edges.Select(e => (e.Transition, Box: SagaGraphLayout.LabelBox(e))).ToList();
        for (var i = 0; i < labels.Count; i++)
        for (var j = i + 1; j < labels.Count; j++)
        {
            var (a, b) = (labels[i], labels[j]);
            a.Box.Overlaps(b.Box).Should().BeFalse(
                $"'{a.Transition.EventName}' ({a.Transition.FromState} to {a.Transition.ToState}) and "
                + $"'{b.Transition.EventName}' ({b.Transition.FromState} to {b.Transition.ToState}) are drawn over each other");
        }
    }

    [Fact(DisplayName = "No edge label is placed on top of a node, on a graph that finalizes")]
    public void Edge_Labels_Clear_Node_Boxes_When_Finalizing()
    {
        var result = SagaGraphLayout.Compute(FinalizingOrderGraph());

        foreach (var edge in result.Edges)
        {
            var box = SagaGraphLayout.LabelBox(edge);
            foreach (var node in result.Nodes)
                box.Overlaps(node).Should().BeFalse(
                    $"the '{edge.Transition.EventName}' label on {edge.Transition.FromState} to {edge.Transition.ToState} "
                    + $"is drawn over the {node.State} node");
        }
    }

    [Fact(DisplayName = "A self loop's label clears a node sharing its layer")]
    public void Self_Loop_Label_Clears_Its_Neighbour()
    {
        var graph = Graph(["Initial", "A", "B"],
            new SagaTransitionDefinition("Initial", "A", "Start"),
            new SagaTransitionDefinition("Initial", "B", "Fork"),
            new SagaTransitionDefinition("A", "A", "Retry"));

        var result = SagaGraphLayout.Compute(graph);

        var loop = result.Edges.Single(e => e.IsSelfLoop);
        var box = SagaGraphLayout.LabelBox(loop);
        foreach (var node in result.Nodes)
            box.Overlaps(node).Should().BeFalse($"the Retry label is drawn over the {node.State} node");
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
