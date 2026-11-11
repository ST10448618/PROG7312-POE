using SmartX.Api.Domain.Models;

namespace SmartX.Api.Domain.Collections;

public static class MeshAlgorithms
{
    private static readonly StringComparer Ids = StringComparer.OrdinalIgnoreCase;

    private readonly record struct Frontier(double Cost, string From, string To) : IComparable<Frontier>
    {
        // Cost first, then ids, so ties resolve the same way on every run.
        public int CompareTo(Frontier other)
        {
            var c = Cost.CompareTo(other.Cost);
            if (c != 0) return c;
            c = string.CompareOrdinal(To, other.To);
            return c != 0 ? c : string.CompareOrdinal(From, other.From);
        }
    }

    private static Func<string, bool> Guard(MeshGraph graph, Func<string, bool> isDown) =>
        id => !(graph.TryGetNode(id, out var n) && n.IsGateway) && isDown(id);

    public static double LinkCost(MeshGraph graph, string a, string b) =>
        graph.Neighbors(a).First(n => Ids.Equals(n.Id, b)).Cost;

    public static (Dictionary<string, int> Hops, Dictionary<string, string> Parent) BreadthFirst(
        MeshGraph graph, string start, Func<string, bool> isDown)
    {
        isDown = Guard(graph, isDown);
        var hops = new Dictionary<string, int>(Ids) { [start] = 0 };
        var parent = new Dictionary<string, string>(Ids);
        var queue = new Queue<string>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var n in graph.Neighbors(current))
            {
                if (hops.ContainsKey(n.Id) || isDown(n.Id)) continue;
                hops[n.Id] = hops[current] + 1;
                parent[n.Id] = current;
                queue.Enqueue(n.Id);
            }
        }
        return (hops, parent);
    }

    public static List<SubnetGroup> FindSubnets(MeshGraph graph, Func<string, bool> isDown)
    {
        isDown = Guard(graph, isDown);
        var visited = new HashSet<string>(Ids);
        var groups = new List<SubnetGroup>();

        foreach (var start in graph.Nodes.Select(n => n.Id).OrderBy(id => id, Ids))
        {
            if (isDown(start) || visited.Contains(start)) continue;

            var members = new List<string>();
            var stack = new Stack<string>();
            stack.Push(start);
            visited.Add(start);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                members.Add(current);
                foreach (var n in graph.Neighbors(current))
                    if (!isDown(n.Id) && visited.Add(n.Id)) stack.Push(n.Id);
            }

            members.Sort(Ids);
            groups.Add(new SubnetGroup(members, members.Any(m => graph.GetNode(m).IsGateway)));
        }

        return groups
            .OrderByDescending(g => g.ContainsGateway)
            .ThenByDescending(g => g.Members.Count)
            .ToList();
    }

    public static MstResult Prim(MeshGraph graph, string root, Func<string, bool> isDown)
    {
        isDown = Guard(graph, isDown);
        var reached = new HashSet<string>(Ids) { root };
        var parent = new Dictionary<string, string>(Ids);
        var edges = new List<MeshEdge>();
        var heap = new MinHeap<Frontier>();
        double total = 0;

        void Expand(string id)
        {
            foreach (var n in graph.Neighbors(id))
                if (!reached.Contains(n.Id) && !isDown(n.Id))
                    heap.Push(new Frontier(n.Cost, id, n.Id));
        }

        Expand(root);
        while (heap.TryPop(out var e))
        {
            if (!reached.Add(e.To)) continue; // already connected by a cheaper link

            parent[e.To] = e.From;
            edges.Add(new MeshEdge(e.From, e.To, e.Cost));
            total += e.Cost;
            Expand(e.To);
        }

        return new MstResult(edges, Math.Round(total, 2), parent, reached);
    }

    /// <summary>Follows parent pointers (from BFS or Prim) from a node back to the root.</summary>
    public static MeshRoute TraceRoute(MeshGraph graph, IReadOnlyDictionary<string, string> parent, string root, string id)
    {
        var start = graph.GetNode(id).Id;
        var path = new List<string> { start };
        var current = start;
        double cost = 0;

        while (!Ids.Equals(current, root))
        {
            if (!parent.TryGetValue(current, out var next))
                return new MeshRoute(start, false, new List<string>(), 0, 0);

            cost += LinkCost(graph, current, next);
            path.Add(next);
            current = next;
        }
        return new MeshRoute(start, true, path, Math.Round(cost, 2), path.Count - 1);
    }

    public static List<RouteChange> RouteChanges(
        MeshGraph graph, string root,
        IReadOnlyDictionary<string, string> healthyParent,
        IReadOnlyDictionary<string, string> currentParent,
        Func<string, bool> isDown)
    {
        isDown = Guard(graph, isDown);
        var changes = new List<RouteChange>();

        foreach (var node in graph.Nodes.Where(n => !n.IsGateway && !isDown(n.Id)).OrderBy(n => n.Id, Ids))
        {
            var before = TraceRoute(graph, healthyParent, root, node.Id);
            if (!before.Reachable || !before.Path.Skip(1).Any(isDown)) continue;

            var now = TraceRoute(graph, currentParent, root, node.Id);
            changes.Add(new RouteChange(
                node.Id, before.Path,
                now.Reachable ? now.Path : null,
                now.Reachable ? now.Path[1] : null,
                before.Cost,
                now.Reachable ? now.Cost : null,
                !now.Reachable));
        }
        return changes;
    }
}