using SmartX.Api.Domain.Collections;
using SmartX.Api.Domain.Models;
using SmartX.Api.Infrastructure.Seeding;

namespace SmartX.Tests.Support;

/// <summary>Graphs the mesh tests share: the real demo mesh, and seeded random meshes for property-style checks.</summary>
internal static class TestGraphs
{
    public static string Gateway => MeshTopologyDefinition.GatewayId;

    public static string Sensor(string suffix) => $"AA:BB:CC:00:01:{suffix}";

    public static MeshGraph Demo() => MeshTopologyDefinition.Build();

    public static HashSet<string> Down(params string[] suffixes) =>
        new(suffixes.Select(Sensor), StringComparer.OrdinalIgnoreCase);

    /// <summary>A connected mesh: a random spanning tree first (so it is always connected), then extra random links.</summary>
    public static MeshGraph Random(int seed, int nodes, int extraLinks)
    {
        var rnd = new Random(seed);
        var graph = new MeshGraph();
        graph.AddNode(new MeshNode("N0", "N0", 0, 0, IsGateway: true));
        for (var i = 1; i < nodes; i++)
            graph.AddNode(new MeshNode($"N{i}", $"N{i}", 0, 0));

        var linked = new HashSet<(int, int)>();

        void Link(int a, int b)
        {
            if (a == b || !linked.Add((Math.Min(a, b), Math.Max(a, b)))) return;
            graph.AddEdge($"N{a}", $"N{b}", Math.Round(1 + rnd.NextDouble() * 9, 2));
        }

        for (var i = 1; i < nodes; i++) Link(i, rnd.Next(i));
        for (var k = 0; k < extraLinks; k++) Link(rnd.Next(nodes), rnd.Next(nodes));

        return graph;
    }
}
