using SmartX.Api.Domain.Collections;

namespace SmartX.Api.Services;

public record MeshNodeView(
    string Id, string Label, double X, double Y, bool IsGateway,
    string Category, string Location, string Status, bool IsDown, int Degree, double? LastValue);

public record MeshEdgeView(string From, string To, double Cost, bool Active);

public record MeshSnapshot(
    string GatewayId, int NodeCount, int EdgeCount, int DownCount,
    List<MeshNodeView> Nodes, List<MeshEdgeView> Edges);

public class MeshService
{
    private readonly LiveDeviceRegistry _registry;
    private readonly ActiveFaultTracker _faults;
    private volatile MeshGraph? _graph;

    public MeshService(LiveDeviceRegistry registry, ActiveFaultTracker faults)
    {
        _registry = registry;
        _faults = faults;
    }

    public bool IsReady => _graph is not null;

    public MeshGraph Graph => _graph ?? throw new InvalidOperationException("The mesh topology has not been loaded yet.");

    public void SetGraph(MeshGraph graph) => _graph = graph;

    public bool IsDown(string id)
    {
        if (Graph.TryGetNode(id, out var node) && node.IsGateway) return false; // the gateway is the destination, not a sensor
        return _faults.IsFaulted(id) || _registry.Get(id)?.Status == "Disconnected";
    }

    public MeshSnapshot GetSnapshot()
    {
        var graph = Graph;
        var nodes = new List<MeshNodeView>(graph.NodeCount);

        foreach (var n in graph.Nodes)
        {
            if (n.IsGateway)
            {
                nodes.Add(new MeshNodeView(n.Id, n.Label, n.X, n.Y, true, "Gateway", "Facility A/Gateway",
                    "Gateway", false, graph.Degree(n.Id), null));
                continue;
            }

            var live = _registry.Get(n.Id);
            var down = IsDown(n.Id);
            nodes.Add(new MeshNodeView(n.Id, n.Label, n.X, n.Y, false,
                live?.Category ?? "Unknown", live?.Location ?? "Unknown",
                down ? "Disconnected" : live?.Status ?? "Online",
                down, graph.Degree(n.Id), live?.LastValue));
        }

        var edges = graph.Edges
            .Select(e => new MeshEdgeView(e.From, e.To, e.Cost, !IsDown(e.From) && !IsDown(e.To)))
            .ToList();

        return new MeshSnapshot(graph.Gateway?.Id ?? "", graph.NodeCount, graph.EdgeCount,
            nodes.Count(n => n.IsDown), nodes, edges);
    }
}