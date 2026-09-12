using System.Globalization;

namespace Iris.Sagas.Layout;

public static class SagaGraphLayout
{
    public const double NodeWidth = 150;
    public const double NodeHeight = 44;
    public const double LayerGap = 80;
    public const double NodeGap = 48;
    public const double Padding = 32;
    private const double LoopReach = 56;
    private const double BackEdgeReach = 70;

    // Labels render as start-anchored 11px text, so one grows to the right of its position and
    // sits on it as a baseline. There is no text measurement available at layout time, so the
    // width is an estimate from the character count; it only has to be close enough to keep a
    // label off a node.
    private const double LabelFontSize = 11;
    private const double LabelCharWidth = 6;

    /// <summary>
    /// The area an edge's label text covers. Placement uses this to keep labels off nodes.
    /// </summary>
    public static LabelBounds LabelBox(EdgeLayout edge) => LabelBox(edge.LabelX, edge.LabelY, edge.Transition.EventName);

    private static LabelBounds LabelBox(double x, double y, string eventName)
        => new(x, y - LabelFontSize, x + eventName.Length * LabelCharWidth, y);

    public static SagaGraphLayoutResult Compute(SagaGraph graph)
    {
        if (graph.States.Count == 0)
            return new SagaGraphLayoutResult([], [], 0, 0);

        var names = graph.States.Select(s => s.Name).ToList();
        var initial = graph.States.FirstOrDefault(s => s.IsInitial)?.Name ?? names[0];
        var forward = new Dictionary<string, List<string>>();
        foreach (var name in names) forward[name] = [];
        foreach (var t in graph.Transitions.Where(t => t.FromState != t.ToState && forward.ContainsKey(t.FromState) && forward.ContainsKey(t.ToState)))
            forward[t.FromState].Add(t.ToState);

        var backEdges = FindBackEdges(initial, forward);
        var layers = AssignLayers(initial, names, forward, backEdges);

        var byLayer = names.GroupBy(n => layers[n]).OrderBy(g => g.Key).ToList();
        var widest = byLayer.Max(g => g.Count());
        var canvasWidth = Padding * 2 + widest * NodeWidth + (widest - 1) * NodeGap + BackEdgeReach;
        var nodes = new List<NodeLayout>();
        foreach (var layer in byLayer)
        {
            var count = layer.Count();
            var rowWidth = count * NodeWidth + (count - 1) * NodeGap;
            var startX = Padding + (widest * NodeWidth + (widest - 1) * NodeGap - rowWidth) / 2;
            var y = Padding + layer.Key * (NodeHeight + LayerGap);
            var index = 0;
            foreach (var name in layer)
                nodes.Add(new NodeLayout(name, startX + index++ * (NodeWidth + NodeGap), y, NodeWidth, NodeHeight));
        }
        var canvasHeight = Padding * 2 + byLayer.Count * NodeHeight + (byLayer.Count - 1) * LayerGap;

        var lookup = nodes.ToDictionary(n => n.State);
        var edges = new List<EdgeLayout>();
        var labelSlots = new Dictionary<(string, string), int>();
        var placedLabels = new List<LabelBounds>();
        foreach (var t in graph.Transitions)
        {
            if (!lookup.TryGetValue(t.FromState, out var from) || !lookup.TryGetValue(t.ToState, out var to))
                continue;

            var slot = labelSlots.GetValueOrDefault((t.FromState, t.ToState));
            labelSlots[(t.FromState, t.ToState)] = slot + 1;
            var labelOffset = slot * 14;

            EdgeLayout edge;
            if (t.FromState == t.ToState)
            {
                var (path, lx, ly) = SelfLoop(from, t.EventName, labelOffset, nodes, placedLabels);
                edge = new EdgeLayout(t, path, lx, ly, false, true);
            }
            else if (backEdges.Contains((t.FromState, t.ToState)))
            {
                var (path, lx, ly) = BackEdge(from, to);
                edge = new EdgeLayout(t, path, lx, ly + labelOffset, true, false);
            }
            else
            {
                var (path, lx, ly) = ForwardEdge(from, to, t.EventName, labelOffset, nodes, placedLabels);
                edge = new EdgeLayout(t, path, lx, ly, false, false);
            }

            edges.Add(edge);
            placedLabels.Add(LabelBox(edge));
        }

        // Transitions sharing a state pair stack their labels downward via labelOffset,
        // so the layer-based canvas height above is only a floor; grow it to whatever
        // the lowest stacked label actually needs, or its own bottom padding is lost.
        if (edges.Count > 0)
            canvasHeight = Math.Max(canvasHeight, edges.Max(e => e.LabelY) + Padding);

        return new SagaGraphLayoutResult(nodes, edges, canvasWidth, canvasHeight);
    }

    // Standard cycle-aware DFS: an edge to a node still on the current recursion
    // stack closes a cycle back toward an ancestor, so it is a back edge. Sweeping
    // the leftover nodes afterward catches states the initial state cannot reach at
    // all. The snapshot of leftover nodes goes stale as the sweep runs (visiting one
    // can finish a later one in the same snapshot), so each iteration re-checks
    // `done` immediately before visiting; that keeps every node's DFS starting exactly
    // once, so a forward edge into an already-explored subtree can never be
    // relabelled as a back edge.
    private static HashSet<(string, string)> FindBackEdges(string initial, Dictionary<string, List<string>> forward)
    {
        var back = new HashSet<(string, string)>();
        var onStack = new HashSet<string>();
        var done = new HashSet<string>();

        void Visit(string node)
        {
            onStack.Add(node);
            foreach (var next in forward[node])
            {
                if (onStack.Contains(next)) back.Add((node, next));
                else if (!done.Contains(next)) Visit(next);
            }
            onStack.Remove(node);
            done.Add(node);
        }

        Visit(initial);
        foreach (var node in forward.Keys.Where(n => !done.Contains(n)).ToList())
            if (!done.Contains(node)) Visit(node);
        return back;
    }

    // A state's layer is one below its deepest predecessor's layer (longest path from
    // Initial), so a state fed by several branches lines up under the branch that took
    // longest to reach it rather than the shortest. Back edges are excluded from
    // "incoming" so a retry/cycle transition can never pull its target back up a layer.
    // The visiting set breaks recursion on a residual cycle among states unreachable
    // from Initial; states that never resolve to a real layer fall through to -1 and
    // are swept into one extra bottom layer below everything else.
    private static Dictionary<string, int> AssignLayers(string initial, List<string> names, Dictionary<string, List<string>> forward, HashSet<(string, string)> backEdges)
    {
        var layers = new Dictionary<string, int> { [initial] = 0 };
        var incoming = names.ToDictionary(n => n, _ => new List<string>());
        foreach (var (from, targets) in forward)
        foreach (var to in targets.Where(to => !backEdges.Contains((from, to))))
            incoming[to].Add(from);

        int Layer(string node, HashSet<string> visiting)
        {
            if (layers.TryGetValue(node, out var known)) return known;
            if (!visiting.Add(node)) return 0;
            var predecessors = incoming[node].Where(p => p == initial || Reachable(initial, p, forward, backEdges)).ToList();
            var layer = predecessors.Count == 0 ? -1 : predecessors.Max(p => Layer(p, visiting)) + 1;
            visiting.Remove(node);
            layers[node] = layer;
            return layer;
        }

        foreach (var name in names) Layer(name, []);

        var deepest = layers.Values.Max();
        foreach (var name in names.Where(n => layers[n] < 0))
            layers[name] = deepest + 1;
        return layers;
    }

    private static bool Reachable(string from, string target, Dictionary<string, List<string>> forward, HashSet<(string, string)> backEdges)
    {
        var seen = new HashSet<string>();
        var stack = new Stack<string>([from]);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node == target) return true;
            if (!seen.Add(node)) continue;
            foreach (var next in forward[node].Where(n => !backEdges.Contains((node, n))))
                stack.Push(next);
        }
        return false;
    }

    // Walked outward from the midpoint, so a label that already sits clear keeps the position
    // that reads best and only a blocked one moves, by as little as possible.
    private static readonly double[] LabelSamples = [0.5, 0.42, 0.58, 0.34, 0.66, 0.26, 0.74, 0.18, 0.82];

    private static (string Path, double LabelX, double LabelY) ForwardEdge(
        NodeLayout from, NodeLayout to, string eventName, double labelOffset,
        List<NodeLayout> nodes, List<LabelBounds> placed)
    {
        var (x1, y1) = (from.CenterX, from.Bottom);
        var (x2, y2) = (to.CenterX, to.Y);
        var bend = Math.Max(24, (y2 - y1) / 2);
        var path = Cubic(x1, y1, x1, y1 + bend, x2, y2 - bend, x2, y2);

        // An edge between non adjacent layers runs through the rows in between, so its midpoint
        // can land on a node belonging to some unrelated transition. Two edges leaving one state
        // for different targets then dodge that node into the same gap and land on each other,
        // so a spot has to be free of both before it is worth taking.
        foreach (var sample in LabelSamples)
        {
            var (sx, sy) = CubicPoint(sample, x1, y1, x1, y1 + bend, x2, y2 - bend, x2, y2);
            var (lx, ly) = (sx + 6, sy + labelOffset);
            if (IsClear(lx, ly, eventName, nodes, placed))
                return (path, lx, ly);
        }

        var (mx, my) = CubicPoint(0.5, x1, y1, x1, y1 + bend, x2, y2 - bend, x2, y2);
        return (path, mx + 6, my + labelOffset);
    }

    private static bool IsClear(double x, double y, string eventName, List<NodeLayout> nodes, List<LabelBounds> placed)
    {
        var box = LabelBox(x, y, eventName);
        return !nodes.Any(box.Overlaps) && !placed.Any(box.Overlaps);
    }

    private static (string Path, double LabelX, double LabelY) BackEdge(NodeLayout from, NodeLayout to)
    {
        var (x1, y1) = (from.Right, from.CenterY);
        var (x2, y2) = (to.Right, to.CenterY);
        var reach = Math.Max(from.Right, to.Right) + BackEdgeReach;
        var path = Cubic(x1, y1, reach, y1, reach, y2, x2, y2);
        var (lx, ly) = CubicPoint(0.5, x1, y1, reach, y1, reach, y2, x2, y2);
        return (path, lx + 6, ly);
    }

    private static (string Path, double LabelX, double LabelY) SelfLoop(
        NodeLayout node, string eventName, double labelOffset,
        List<NodeLayout> nodes, List<LabelBounds> placed)
    {
        var (x, y1, y2) = (node.Right, node.Y + node.Height * 0.3, node.Y + node.Height * 0.7);
        var reach = x + LoopReach;
        var path = Cubic(x, y1, reach, y1 - 28, reach, y2 + 28, x, y2);

        var (besideX, besideY) = (reach - 8, node.CenterY + labelOffset);
        if (IsClear(besideX, besideY, eventName, nodes, placed))
            return (path, besideX, besideY);

        // The loop occupies the gap beside its node, which is exactly where the next node in the
        // same layer starts. Sit the label above the arc instead, stacking upward so a second
        // loop on the same state clears the node rather than dropping back onto it.
        return (path, x + 4, node.Y - 4 - labelOffset);
    }

    private static string Cubic(double x1, double y1, double c1x, double c1y, double c2x, double c2y, double x2, double y2)
        => string.Create(CultureInfo.InvariantCulture,
            $"M {x1:0.#} {y1:0.#} C {c1x:0.#} {c1y:0.#}, {c2x:0.#} {c2y:0.#}, {x2:0.#} {y2:0.#}");

    private static (double X, double Y) CubicPoint(double t, double x1, double y1, double c1x, double c1y, double c2x, double c2y, double x2, double y2)
    {
        var u = 1 - t;
        var x = u * u * u * x1 + 3 * u * u * t * c1x + 3 * u * t * t * c2x + t * t * t * x2;
        var y = u * u * u * y1 + 3 * u * u * t * c1y + 3 * u * t * t * c2y + t * t * t * y2;
        return (x, y);
    }
}
