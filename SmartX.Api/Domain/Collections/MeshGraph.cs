using SmartX.Api.Domain.Models;

namespace SmartX.Api.Domain.Collections;

public class MeshGraph
{
    private readonly Dictionary<string, MeshNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<MeshNeighbor>> _adjacency = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MeshEdge> _edges = new();

    public int NodeCount => _nodes.Count;
    public int EdgeCount => _edges.Count;
    public IReadOnlyCollection<MeshNode> Nodes => _nodes.Values;
    public IReadOnlyList<MeshEdge> Edges => _edges;

    public MeshNode? Gateway => _nodes.Values.FirstOrDefault(n => n.IsGateway);

    public bool Contains(string id) => _nodes.ContainsKey(id);

    public bool TryGetNode(string id, out MeshNode node) => _nodes.TryGetValue(id, out node!);

    public MeshNode GetNode(string id) =>
        _nodes.TryGetValue(id, out var node) ? node : throw new KeyNotFoundException($"Node '{id}' is not in the mesh.");

    public IReadOnlyList<MeshNeighbor> Neighbors(string id) =>
        _adjacency.TryGetValue(id, out var list) ? list : throw new KeyNotFoundException($"Node '{id}' is not in the mesh.");

    public int Degree(string id) => Neighbors(id).Count;

    public void AddNode(MeshNode node)
    {
        if (string.IsNullOrWhiteSpace(node.Id))
            throw new ArgumentException("A mesh node needs an identifier.");
        if (_nodes.ContainsKey(node.Id))
            throw new ArgumentException($"Node '{node.Id}' is already in the mesh.");
        if (node.IsGateway && Gateway is not null)
            throw new ArgumentException("The mesh already has a gateway.");

        _nodes[node.Id] = node;
        _adjacency[node.Id] = new List<MeshNeighbor>();
    }

    public void AddEdge(string from, string to, double cost)
    {
        if (!_nodes.TryGetValue(from, out var a)) throw new ArgumentException($"Unknown node '{from}'.");
        if (!_nodes.TryGetValue(to, out var b)) throw new ArgumentException($"Unknown node '{to}'.");
        if (a.Id.Equals(b.Id, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("A node cannot link to itself.");
        if (!double.IsFinite(cost) || cost <= 0) throw new ArgumentException("A link cost must be a positive number.");
        if (_adjacency[a.Id].Any(n => n.Id.Equals(b.Id, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"A link between '{a.Id}' and '{b.Id}' already exists.");

        _adjacency[a.Id].Add(new MeshNeighbor(b.Id, cost));
        _adjacency[b.Id].Add(new MeshNeighbor(a.Id, cost));
        _edges.Add(new MeshEdge(a.Id, b.Id, cost));
    }
}