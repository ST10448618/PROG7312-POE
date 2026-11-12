using SmartX.Api.Domain.Collections;

namespace SmartX.Tests.Support;

/// <summary>
/// An independent minimum-spanning-tree implementation (Kruskal with union-find) used only to cross-check Prim.
/// Two different algorithms agreeing on the total weight is strong evidence that both are right.
/// </summary>
internal static class KruskalReference
{
    public static (double Total, int EdgeCount) Run(MeshGraph graph, IEnumerable<string> members)
    {
        var memberSet = new HashSet<string>(members, StringComparer.OrdinalIgnoreCase);
        var parent = memberSet.ToDictionary(m => m, m => m, StringComparer.OrdinalIgnoreCase);

        string Find(string x)
        {
            while (!parent[x].Equals(x, StringComparison.OrdinalIgnoreCase))
            {
                parent[x] = parent[parent[x]]; // path halving
                x = parent[x];
            }
            return x;
        }

        double total = 0;
        var count = 0;

        foreach (var e in graph.Edges
                     .Where(e => memberSet.Contains(e.From) && memberSet.Contains(e.To))
                     .OrderBy(e => e.Cost))
        {
            var a = Find(e.From);
            var b = Find(e.To);
            if (a.Equals(b, StringComparison.OrdinalIgnoreCase)) continue;

            parent[a] = b;
            total += e.Cost;
            count++;
        }

        return (total, count);
    }
}
