using SmartX.Api.Domain.Collections;
using SmartX.Api.Domain.Models;

namespace SmartX.Api.Services;

public record MstView(List<MeshEdge> Edges, double TotalCost, int NodesReached);

public record MeshAnalysis(
    string GatewayId, int NodeCount, List<string> DownNodes, int ReachableCount, List<string> Stranded,
    List<SubnetGroup> Subnets, MstView Mst, double ActiveLinkCost, double MstSavingsPercent,
    List<RouteChange> Reroutes, DateTime GeneratedAt);

public record RouteReport(string Id, bool IsDown, MeshRoute MstRoute, MeshRoute FewestHopsRoute, bool RoutesDiffer);

public class MeshAnalysisService
{
    private readonly MeshService _mesh;

    public MeshAnalysisService(MeshService mesh) => _mesh = mesh;

    public List<string> UnknownIds(IEnumerable<string> ids) =>
        ids.Where(id => !_mesh.Graph.Contains(id)).ToList();

    /// <summary>Live down nodes plus any "what if" nodes the caller wants to treat as down.</summary>
    private Func<string, bool> SnapshotDown(IEnumerable<string>? assumeDown)
    {
        var graph = _mesh.Graph;
        var assumed = new HashSet<string>(assumeDown ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var down = new HashSet<string>(
            graph.Nodes.Where(n => !n.IsGateway && (assumed.Contains(n.Id) || _mesh.IsDown(n.Id))).Select(n => n.Id),
            StringComparer.OrdinalIgnoreCase);
        return id => down.Contains(id);
    }

    public MeshAnalysis Analyze(IEnumerable<string>? assumeDown = null)
    {
        var graph = _mesh.Graph;
        var gateway = graph.Gateway ?? throw new InvalidOperationException("The mesh has no gateway.");
        var isDown = SnapshotDown(assumeDown);

        var mst = MeshAlgorithms.Prim(graph, gateway.Id, isDown);
        var healthy = MeshAlgorithms.Prim(graph, gateway.Id, _ => false);
        var subnets = MeshAlgorithms.FindSubnets(graph, isDown);

        var down = graph.Nodes.Where(n => isDown(n.Id)).Select(n => n.Id)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
        var stranded = graph.Nodes
            .Where(n => !n.IsGateway && !isDown(n.Id) && !mst.Reached.Contains(n.Id))
            .Select(n => n.Id).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();

        // Cost of keeping every usable link in the gateway's network transmitting, against only the MST links.
        var activeCost = graph.Edges
            .Where(e => mst.Reached.Contains(e.From) && mst.Reached.Contains(e.To))
            .Sum(e => e.Cost);
        var savings = activeCost > 0 ? (activeCost - mst.TotalCost) / activeCost * 100 : 0;

        return new MeshAnalysis(
            gateway.Id, graph.NodeCount, down, mst.Reached.Count - 1, stranded, subnets,
            new MstView(mst.Edges, mst.TotalCost, mst.Reached.Count),
            Math.Round(activeCost, 2), Math.Round(savings, 2),
            MeshAlgorithms.RouteChanges(graph, gateway.Id, healthy.Parent, mst.Parent, isDown),
            DateTime.UtcNow);
    }

    /// <summary>Route for one node by the MST (cheapest total network) and by BFS (fewest hops). Null if unknown.</summary>
    public RouteReport? Route(string id, IEnumerable<string>? assumeDown = null)
    {
        var graph = _mesh.Graph;
        if (!graph.TryGetNode(id, out var node)) return null;

        var gateway = graph.Gateway ?? throw new InvalidOperationException("The mesh has no gateway.");
        var isDown = SnapshotDown(assumeDown);

        var mst = MeshAlgorithms.Prim(graph, gateway.Id, isDown);
        var bfs = MeshAlgorithms.BreadthFirst(graph, gateway.Id, isDown);

        var mstRoute = MeshAlgorithms.TraceRoute(graph, mst.Parent, gateway.Id, node.Id);
        var hopRoute = MeshAlgorithms.TraceRoute(graph, bfs.Parent, gateway.Id, node.Id);

        var differ = mstRoute.Reachable && hopRoute.Reachable &&
                     !mstRoute.Path.SequenceEqual(hopRoute.Path, StringComparer.OrdinalIgnoreCase);

        return new RouteReport(node.Id, isDown(node.Id), mstRoute, hopRoute, differ);
    }
}