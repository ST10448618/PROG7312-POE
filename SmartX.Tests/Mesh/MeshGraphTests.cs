using SmartX.Api.Domain.Collections;
using SmartX.Api.Domain.Models;
using SmartX.Tests.Support;

namespace SmartX.Tests.Mesh;

public class MeshGraphTests
{
    private static MeshGraph Triangle()
    {
        var g = new MeshGraph();
        g.AddNode(new MeshNode("GW", "GW", 0, 0, IsGateway: true));
        g.AddNode(new MeshNode("A", "A", 1, 1));
        g.AddNode(new MeshNode("B", "B", 2, 2));
        return g;
    }

    [Fact]
    public void DemoMesh_HasTheDocumentedShape()
    {
        var g = TestGraphs.Demo();

        Assert.Equal(13, g.NodeCount);  // 12 sensors + the gateway
        Assert.Equal(23, g.EdgeCount);
        Assert.Equal(TestGraphs.Gateway, g.Gateway!.Id);
        Assert.Equal(1, g.Nodes.Count(n => n.IsGateway));
    }

    [Fact]
    public void DemoMesh_IsFullyLinked_NoNodeIsAnIsland()
    {
        var g = TestGraphs.Demo();

        foreach (var node in g.Nodes)
            Assert.True(g.Degree(node.Id) >= 2, $"{node.Id} has fewer than two links, so one failure could strand it.");
    }

    [Fact]
    public void Links_AreUndirected_WithTheSameCostBothWays()
    {
        var g = Triangle();
        g.AddEdge("A", "B", 2.5);

        Assert.Contains(g.Neighbors("A"), n => n.Id == "B" && n.Cost == 2.5);
        Assert.Contains(g.Neighbors("B"), n => n.Id == "A" && n.Cost == 2.5);
        Assert.Equal(1, g.EdgeCount);
        Assert.Equal(1, g.Degree("A"));
    }

    [Fact]
    public void Lookups_IgnoreCase()
    {
        var g = TestGraphs.Demo();

        Assert.True(g.Contains("aa:bb:cc:00:01:05"));
        Assert.True(g.TryGetNode("aa:bb:cc:00:01:05", out var node));
        Assert.Equal("AA:BB:CC:00:01:05", node.Id);
    }

    [Fact]
    public void UnknownNode_ThrowsKeyNotFound()
    {
        var g = Triangle();

        Assert.Throws<KeyNotFoundException>(() => g.GetNode("nope"));
        Assert.Throws<KeyNotFoundException>(() => g.Neighbors("nope"));
        Assert.False(g.TryGetNode("nope", out _));
    }

    [Fact]
    public void AddNode_RejectsDuplicatesBlankIdsAndASecondGateway()
    {
        var g = Triangle();

        Assert.Throws<ArgumentException>(() => g.AddNode(new MeshNode("A", "A", 0, 0)));
        Assert.Throws<ArgumentException>(() => g.AddNode(new MeshNode("a", "a", 0, 0)));   // ids are case-insensitive
        Assert.Throws<ArgumentException>(() => g.AddNode(new MeshNode(" ", "blank", 0, 0)));
        Assert.Throws<ArgumentException>(() => g.AddNode(new MeshNode("GW2", "GW2", 0, 0, IsGateway: true)));
    }

    [Fact]
    public void AddEdge_RejectsUnknownNodesSelfLinksAndDuplicates()
    {
        var g = Triangle();
        g.AddEdge("A", "B", 1);

        Assert.Throws<ArgumentException>(() => g.AddEdge("A", "missing", 1));
        Assert.Throws<ArgumentException>(() => g.AddEdge("missing", "A", 1));
        Assert.Throws<ArgumentException>(() => g.AddEdge("A", "A", 1));
        Assert.Throws<ArgumentException>(() => g.AddEdge("A", "B", 2));
        Assert.Throws<ArgumentException>(() => g.AddEdge("B", "A", 2));   // same link, opposite direction
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AddEdge_RejectsCostsThatWouldBreakPathfinding(double cost)
    {
        var g = Triangle();

        Assert.Throws<ArgumentException>(() => g.AddEdge("A", "B", cost));
        Assert.Equal(0, g.EdgeCount);
    }
}
