using Bunit;
using FluentAssertions;
using Iris.Components.Sagas;
using Iris.Sagas;

namespace Iris.Components.Test.Sagas;

public class SagaGraphViewTests : IrisTestContext
{
    private static SagaGraph OrderGraph() => new("Sample.OrderStateMachine", "OrderStateMachine", "MassTransit",
        [
            new SagaState("Initial", true, false),
            new SagaState("Submitted", false, false),
            new SagaState("Accepted", false, false),
            new SagaState("Final", false, true),
        ],
        [
            new SagaTransitionDefinition("Initial", "Submitted", "SubmitOrder"),
            new SagaTransitionDefinition("Submitted", "Accepted", "AcceptOrder"),
            new SagaTransitionDefinition("Accepted", "Final", "ShipOrder"),
        ]);

    private static SagaInstance InstanceAtAccepted()
    {
        var id = Guid.NewGuid();
        var t0 = DateTimeOffset.UtcNow;
        return new SagaInstance(id, "Sample.OrderStateMachine", "Accepted", t0, t0.AddSeconds(1),
        [
            new SagaTransition("OrderStateMachine", id, "Initial", "Submitted", "SubmitOrder", "t", "s1", t0),
            new SagaTransition("OrderStateMachine", id, "Submitted", "Accepted", "AcceptOrder", "t", "s2", t0.AddSeconds(1)),
        ]);
    }

    [Fact(DisplayName = "Edge labels paint after the nodes so a node's fill can never hide one")]
    public void Labels_Paint_Above_Nodes()
    {
        var cut = RenderComponent<SagaGraphView>(p => p.Add(x => x.Graph, OrderGraph()));

        var painted = cut.Find("svg.saga-graph").QuerySelectorAll("g.node, text.edge-label").ToList();
        var lastNode = painted.FindLastIndex(e => e.ClassList.Contains("node"));
        var firstLabel = painted.FindIndex(e => e.ClassList.Contains("edge-label"));

        firstLabel.Should().BeGreaterThan(lastNode,
            "SVG paints in document order, so a label written before the nodes disappears behind whichever node it lands on");
    }

    [Fact(DisplayName = "Renders one node per state and one edge per transition")]
    public void Renders_Nodes_And_Edges()
    {
        var cut = RenderComponent<SagaGraphView>(p => p.Add(x => x.Graph, OrderGraph()));

        cut.FindAll("g.node").Should().HaveCount(4);
        cut.FindAll("path.edge").Should().HaveCount(3);
        cut.Find("g.node[data-state='Initial']").ClassList.Should().Contain("node-initial");
        cut.Find("g.node[data-state='Final']").ClassList.Should().Contain("node-final");
        cut.Markup.Should().Contain("SubmitOrder");
    }

    [Fact(DisplayName = "Selected instance highlights visited edges, visited states and the current state")]
    public void Highlights_Selected_Instance()
    {
        var cut = RenderComponent<SagaGraphView>(p => p
            .Add(x => x.Graph, OrderGraph())
            .Add(x => x.SelectedInstance, InstanceAtAccepted()));

        cut.Find("g.node[data-state='Accepted']").ClassList.Should().Contain("node-current");
        cut.Find("g.node[data-state='Submitted']").ClassList.Should().Contain("node-visited");
        cut.Find("g.node[data-state='Final']").ClassList.Should().NotContain("node-visited");
        cut.Find("path.edge[data-event='AcceptOrder']").ClassList.Should().Contain("edge-visited").And.Contain("edge-latest");
        cut.Find("path.edge[data-event='SubmitOrder']").ClassList.Should().Contain("edge-visited").And.NotContain("edge-latest");
        cut.Find("path.edge[data-event='ShipOrder']").ClassList.Should().NotContain("edge-visited");
    }

    [Fact(DisplayName = "Clearing the selection removes highlights")]
    public void Clearing_Selection_Removes_Highlights()
    {
        var cut = RenderComponent<SagaGraphView>(p => p
            .Add(x => x.Graph, OrderGraph())
            .Add(x => x.SelectedInstance, InstanceAtAccepted()));

        cut.SetParametersAndRender(p => p.Add(x => x.SelectedInstance, null));

        cut.FindAll("g.node-current").Should().BeEmpty();
        cut.FindAll("path.edge-visited").Should().BeEmpty();
    }

    [Fact(DisplayName = "Edge highlights on state pair even when the graph's event name differs from the live transition's event name")]
    public void Highlights_Edge_When_Event_Names_Differ()
    {
        // The graph definition names the event "OrderAccepted" (the state machine's event
        // property), while the live transition names it "AcceptOrder" (the message type
        // parsed from the URN). These come from different sources and routinely disagree,
        // so matching must key on the state pair alone or nothing would ever highlight.
        var graph = new SagaGraph("Sample.OrderStateMachine", "OrderStateMachine", "MassTransit",
            [
                new SagaState("Initial", true, false),
                new SagaState("Submitted", false, false),
                new SagaState("Accepted", false, false),
                new SagaState("Final", false, true),
            ],
            [
                new SagaTransitionDefinition("Initial", "Submitted", "SubmitOrder"),
                new SagaTransitionDefinition("Submitted", "Accepted", "OrderAccepted"),
                new SagaTransitionDefinition("Accepted", "Final", "ShipOrder"),
            ]);

        var id = Guid.NewGuid();
        var t0 = DateTimeOffset.UtcNow;
        var instance = new SagaInstance(id, "Sample.OrderStateMachine", "Accepted", t0, t0.AddSeconds(1),
        [
            new SagaTransition("OrderStateMachine", id, "Initial", "Submitted", "SubmitOrder", "t", "s1", t0),
            new SagaTransition("OrderStateMachine", id, "Submitted", "Accepted", "AcceptOrder", "t", "s2", t0.AddSeconds(1)),
        ]);

        var cut = RenderComponent<SagaGraphView>(p => p
            .Add(x => x.Graph, graph)
            .Add(x => x.SelectedInstance, instance));

        cut.Find("path.edge[data-event='OrderAccepted']").ClassList.Should().Contain("edge-visited").And.Contain("edge-latest");
    }
}
