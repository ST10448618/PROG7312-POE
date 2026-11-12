using SmartX.Api.Domain.Collections;
using SmartX.Api.Infrastructure.Seeding;
using SmartX.Tests.Support;
using static SmartX.Tests.Support.TestGraphs;

namespace SmartX.Tests.Mesh;

public class MeshAlgorithmsTests
{
    private static readonly Func<string, bool> AllUp = _ => false;

    private static Func<string, bool> DownSet(params string[] suffixes)
    {
        var set = Down(suffixes);
        return set.Contains;
    }

    public static IEnumerable<object[]> SensorSuffixes =>
        MeshTopologyDefinition.Sensors.Select(s => new object[] { s.Mac[^2..] });

    // ------------------------------------------------------------------- Prim

    [Fact]
    public void Prim_OnTheHealthyMesh_SpansEveryNodeWithTwelveLinks()
    {
        var g = Demo();

        var mst = MeshAlgorithms.Prim(g, Gateway, AllUp);

        Assert.Equal(13, mst.Reached.Count);
        Assert.Equal(12, mst.Edges.Count);      // a spanning tree over n nodes always has n - 1 links
        Assert.Equal(29.6, mst.TotalCost, 0.0001);
    }

    [Fact]
    public void Prim_BeatsKeepingEveryLinkOn()
    {
        var g = Demo();

        var mst = MeshAlgorithms.Prim(g, Gateway, AllUp);
        var everyLink = g.Edges.Sum(e => e.Cost);

        Assert.True(mst.TotalCost < everyLink);
    }

    [Fact]
    public void Prim_IsDeterministic()
    {
        var g = Demo();

        var first = MeshAlgorithms.Prim(g, Gateway, AllUp);
        var second = MeshAlgorithms.Prim(g, Gateway, AllUp);

        Assert.Equal(first.Edges, second.Edges);
        Assert.Equal(first.TotalCost, second.TotalCost);
    }

    [Fact]
    public void Prim_TreeIsSpanning_AndEveryRouteEndsAtTheGateway()
    {
        var g = Demo();

        var mst = MeshAlgorithms.Prim(g, Gateway, AllUp);

        Assert.Equal(mst.Reached.Count - 1, mst.Parent.Count);
        foreach (var node in g.Nodes)
        {
            var route = MeshAlgorithms.TraceRoute(g, mst.Parent, Gateway, node.Id);
            Assert.True(route.Reachable, $"{node.Id} has no route to the gateway.");
            Assert.Equal(Gateway, route.Path[^1], ignoreCase: true);
            Assert.Equal(route.Path.Count, route.Path.Distinct(StringComparer.OrdinalIgnoreCase).Count()); // no loops
        }
    }

    [Theory]
    [MemberData(nameof(SensorSuffixes))]
    public void Prim_MatchesKruskal_WhenAnySingleSensorFails(string suffix)
    {
        var g = Demo();
        var isDown = DownSet(suffix);

        var prim = MeshAlgorithms.Prim(g, Gateway, isDown);
        var reachable = MeshAlgorithms.BreadthFirst(g, Gateway, isDown).Hops.Keys;
        var kruskal = KruskalReference.Run(g, reachable);

        Assert.Equal(kruskal.Total, prim.TotalCost, 0.0001);
        Assert.Equal(kruskal.EdgeCount, prim.Edges.Count);
        Assert.DoesNotContain(Sensor(suffix), prim.Reached, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prim_MatchesKruskal_OnRandomMeshesWithRandomFailures()
    {
        for (var seed = 1; seed <= 40; seed++)
        {
            var g = TestGraphs.Random(seed, nodes: 30, extraLinks: 45);
            var rnd = new System.Random(seed * 31);
            var down = g.Nodes.Where(n => !n.IsGateway && rnd.NextDouble() < 0.25)
                .Select(n => n.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var prim = MeshAlgorithms.Prim(g, "N0", down.Contains);
            var reachable = MeshAlgorithms.BreadthFirst(g, "N0", down.Contains).Hops.Keys;
            var kruskal = KruskalReference.Run(g, reachable);

            Assert.True(Math.Abs(kruskal.Total - prim.TotalCost) < 0.0001,
                $"Seed {seed}: Prim {prim.TotalCost} vs Kruskal {kruskal.Total}.");
            Assert.Equal(kruskal.EdgeCount, prim.Edges.Count);
            Assert.Equal(reachable.Count, prim.Reached.Count);
        }
    }

    [Fact]
    public void Prim_NeverTreatsTheGatewayAsDown()
    {
        var g = Demo();

        var mst = MeshAlgorithms.Prim(g, Gateway, _ => true); // "everything is down", gateway included

        Assert.Single(mst.Reached);
        Assert.Empty(mst.Edges);
        Assert.Equal(0, mst.TotalCost);
    }

    // -------------------------------------------------------------------- BFS

    [Fact]
    public void BreadthFirst_FindsTheFewestHops()
    {
        var g = Demo();

        var (hops, parent) = MeshAlgorithms.BreadthFirst(g, Gateway, AllUp);

        var expected = new Dictionary<string, int>
        {
            ["03"] = 1, ["05"] = 1, ["07"] = 1,
            ["01"] = 2, ["04"] = 2, ["06"] = 2, ["08"] = 2,
            ["02"] = 3, ["09"] = 3, ["0A"] = 3,
            ["0B"] = 4, ["0C"] = 4,
        };

        Assert.Equal(0, hops[Gateway]);
        foreach (var (suffix, count) in expected)
            Assert.Equal(count, hops[Sensor(suffix)]);
        Assert.Equal(13, hops.Count);
        Assert.Equal(12, parent.Count);
    }

    [Fact]
    public void BreadthFirst_RoutesAroundADownNode()
    {
        var g = Demo();

        var (hops, _) = MeshAlgorithms.BreadthFirst(g, Gateway, DownSet("05"));

        Assert.DoesNotContain(Sensor("05"), hops.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(12, hops.Count);                 // every other node, including the gateway, is still reachable
        Assert.Equal(2, hops[Sensor("06")]);          // gateway -> 07 -> 06
    }

    [Fact]
    public void BreadthFirst_StopsAtDownNodes_SoTheyCutOffWhatIsBehindThem()
    {
        var g = Demo();

        var (hops, _) = MeshAlgorithms.BreadthFirst(g, Gateway, DownSet("03", "05", "07"));

        Assert.Equal(new[] { Gateway }, hops.Keys);
    }

    [Fact]
    public void BreadthFirst_UnknownStart_Throws()
    {
        var g = Demo();

        Assert.Throws<KeyNotFoundException>(() => MeshAlgorithms.BreadthFirst(g, "no-such-node", AllUp));
    }

    // ------------------------------------------------------------ TraceRoute

    [Fact]
    public void TraceRoute_OnTheTree_FollowsTheCheapestChainForTheFarthestNode()
    {
        var g = Demo();
        var mst = MeshAlgorithms.Prim(g, Gateway, AllUp);

        var route = MeshAlgorithms.TraceRoute(g, mst.Parent, Gateway, Sensor("0B"));

        Assert.True(route.Reachable);
        Assert.Equal(6, route.Hops);
        Assert.Equal(14.0, route.Cost, 0.0001);
        Assert.Equal(Sensor("0B"), route.Path[0]);
        Assert.Equal(Gateway, route.Path[^1]);

        // Every consecutive pair on the route is a real link, and the costs add up.
        double sum = 0;
        for (var i = 1; i < route.Path.Count; i++)
            sum += MeshAlgorithms.LinkCost(g, route.Path[i - 1], route.Path[i]);
        Assert.Equal(route.Cost, sum, 0.0001);
    }

    [Fact]
    public void TraceRoute_ForTheGateway_IsZeroHops()
    {
        var g = Demo();
        var mst = MeshAlgorithms.Prim(g, Gateway, AllUp);

        var route = MeshAlgorithms.TraceRoute(g, mst.Parent, Gateway, Gateway);

        Assert.True(route.Reachable);
        Assert.Equal(0, route.Hops);
        Assert.Equal(0, route.Cost);
        Assert.Single(route.Path);
    }

    [Fact]
    public void TraceRoute_ForANodeOutsideTheTree_IsUnreachable()
    {
        var g = Demo();
        var mst = MeshAlgorithms.Prim(g, Gateway, DownSet("03", "05", "07"));

        var route = MeshAlgorithms.TraceRoute(g, mst.Parent, Gateway, Sensor("01"));

        Assert.False(route.Reachable);
        Assert.Empty(route.Path);
        Assert.Equal(0, route.Hops);
    }

    [Fact]
    public void LinkCost_ReturnsTheCostRegardlessOfDirection()
    {
        var g = Demo();

        Assert.Equal(2.0, MeshAlgorithms.LinkCost(g, Gateway, Sensor("05")));
        Assert.Equal(2.0, MeshAlgorithms.LinkCost(g, Sensor("05"), Gateway));
    }

    // ------------------------------------------------- self-healing / reroutes

    [Fact]
    public void WhenNode05Fails_TheTreeShrinksByOneLink_AndEveryoneStillReachesTheGateway()
    {
        var g = Demo();
        var isDown = DownSet("05");

        var mst = MeshAlgorithms.Prim(g, Gateway, isDown);

        Assert.Equal(12, mst.Reached.Count);   // gateway + the 11 surviving sensors
        Assert.Equal(11, mst.Edges.Count);
        Assert.Equal(29.3, mst.TotalCost, 0.0001);
        Assert.DoesNotContain(Sensor("05"), mst.Reached, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void WhenNode05Fails_ItsDependantsGetANewRouteAndNextHop()
    {
        var g = Demo();
        var isDown = DownSet("05");
        var healthy = MeshAlgorithms.Prim(g, Gateway, AllUp);
        var current = MeshAlgorithms.Prim(g, Gateway, isDown);

        var changes = MeshAlgorithms.RouteChanges(g, Gateway, healthy.Parent, current.Parent, isDown);

        Assert.Equal(11, changes.Count);             // every surviving sensor routed through 05 before
        Assert.All(changes, c => Assert.False(c.Stranded));

        var node03 = changes.Single(c => c.Id.Equals(Sensor("03"), StringComparison.OrdinalIgnoreCase));
        Assert.Equal(new[] { Sensor("03"), Sensor("05"), Gateway }, node03.OldPath);
        Assert.Equal(new[] { Sensor("03"), Gateway }, node03.NewPath);
        Assert.Equal(Gateway, node03.NextHop);
        Assert.Equal(4.4, node03.OldCost, 0.0001);
        Assert.Equal(3.0, node03.NewCost!.Value, 0.0001);
    }

    [Fact]
    public void RouteChanges_AreEmpty_WhenNothingIsDown()
    {
        var g = Demo();
        var healthy = MeshAlgorithms.Prim(g, Gateway, AllUp);

        Assert.Empty(MeshAlgorithms.RouteChanges(g, Gateway, healthy.Parent, healthy.Parent, AllUp));
    }

    [Fact]
    public void WhenEveryGatewayNeighbourFails_TheRestAreStrandedNotRerouted()
    {
        var g = Demo();
        var isDown = DownSet("03", "05", "07");
        var healthy = MeshAlgorithms.Prim(g, Gateway, AllUp);
        var current = MeshAlgorithms.Prim(g, Gateway, isDown);

        Assert.Empty(current.Edges);
        Assert.Single(current.Reached);

        var changes = MeshAlgorithms.RouteChanges(g, Gateway, healthy.Parent, current.Parent, isDown);

        Assert.Equal(9, changes.Count);
        Assert.All(changes, c =>
        {
            Assert.True(c.Stranded);
            Assert.Null(c.NewPath);
            Assert.Null(c.NextHop);
            Assert.Null(c.NewCost);
        });
    }

    // ------------------------------------------------------- DFS: subnets

    [Fact]
    public void FindSubnets_OnTheHealthyMesh_IsOneGroupHoldingTheGateway()
    {
        var g = Demo();

        var groups = MeshAlgorithms.FindSubnets(g, AllUp);

        var only = Assert.Single(groups);
        Assert.True(only.ContainsGateway);
        Assert.Equal(13, only.Members.Count);
    }

    [Fact]
    public void FindSubnets_SplitsOffAnIsolatedLeaf()
    {
        var g = Demo();

        // 0B only links to 02 and 0C, so losing both leaves it alive but cut off.
        var groups = MeshAlgorithms.FindSubnets(g, DownSet("02", "0C"));

        Assert.Equal(2, groups.Count);
        Assert.True(groups[0].ContainsGateway);   // the gateway's group is always listed first
        Assert.Equal(10, groups[0].Members.Count);
        Assert.False(groups[1].ContainsGateway);
        Assert.Equal(new[] { Sensor("0B") }, groups[1].Members);
    }

    [Fact]
    public void FindSubnets_FindsALargeIslandBehindTheDownNodes()
    {
        var g = Demo();

        var groups = MeshAlgorithms.FindSubnets(g, DownSet("03", "05", "07"));

        Assert.Equal(2, groups.Count);
        Assert.Equal(new[] { Gateway }, groups[0].Members);
        Assert.False(groups[1].ContainsGateway);
        Assert.Equal(9, groups[1].Members.Count);
    }

    [Fact]
    public void FindSubnets_NeverIncludesDownNodes()
    {
        var g = Demo();

        var groups = MeshAlgorithms.FindSubnets(g, DownSet("04", "09"));

        var everyone = groups.SelectMany(x => x.Members).ToList();
        Assert.DoesNotContain(Sensor("04"), everyone, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(Sensor("09"), everyone, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(11, everyone.Count);
    }
}
