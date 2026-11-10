using System.Text.Json;
using SmartX.Api.Domain.Entities;

namespace SmartX.Api.Services;

public record TreeValidationResult(bool IsValid, int NodeCount, int MaxDepth, int SensorCount, List<string> Errors);

public class DeploymentTreeService
{
    /// <summary>Deepest hierarchy accepted. Far beyond any real site, but it bounds the work done on hostile input.</summary>
    public const int MaxDepth = 256;

    /// <summary>Each tree level costs two JSON levels (an object plus its "children" array).</summary>
    public const int MaxJsonDepth = MaxDepth * 2 + 8;

    /// <summary>Paths up to this many segments use the recursive check; longer ones switch to a loop.</summary>
    private const int RecursionLimit = 200;

    // ------------------------------------------------------------------ parsing

    public DeploymentNode ParseFromJson(JsonElement element) => ParseFromJson(element, 1);

    private DeploymentNode ParseFromJson(JsonElement element, int depth)
    {
        if (depth > MaxDepth)
            throw new ArgumentException($"Deployment tree is nested deeper than {MaxDepth} levels.");

        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("name", out var nameProp)
            || nameProp.ValueKind != JsonValueKind.String)
            throw new ArgumentException("Every deployment node must be an object with a string 'name'.");

        var node = new DeploymentNode
        {
            Name = nameProp.GetString() ?? "",
            HasSensor = element.TryGetProperty("hasSensor", out var hs)
                        && hs.ValueKind is JsonValueKind.True or JsonValueKind.False
                        && hs.GetBoolean()
        };

        if (element.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
                node.Children.Add(ParseFromJson(child, depth + 1));
        }

        return node;
    }

    // -------------------------------------------------------------- path checks

    /// <summary>
    /// Is there a sensor at the end of this path? <paramref name="startIndex"/> is the first segment *below*
    /// <paramref name="root"/> (the root's own name is checked by the caller). Short paths use the recursive
    /// walk; very long ones switch to a loop so stack depth never depends on request size.
    /// </summary>
    public bool ValidatePath(DeploymentNode root, IReadOnlyList<string> path, int startIndex = 1) =>
        path.Count - startIndex <= RecursionLimit
            ? ValidatePathRecursive(root, path, startIndex)
            : ValidatePathIterative(root, path, startIndex);

    private static bool ValidatePathRecursive(DeploymentNode node, IReadOnlyList<string> path, int index)
    {
        if (index >= path.Count)
            return node.HasSensor; // base case: path consumed, so this node must carry a sensor

        var child = node.Children.FirstOrDefault(c => c.Name.Equals(path[index], StringComparison.OrdinalIgnoreCase));
        return child is not null && ValidatePathRecursive(child, path, index + 1);
    }

    private static bool ValidatePathIterative(DeploymentNode root, IReadOnlyList<string> path, int startIndex)
    {
        var node = root;
        for (var i = startIndex; i < path.Count; i++)
        {
            var segment = path[i];
            var next = node.Children.FirstOrDefault(c => c.Name.Equals(segment, StringComparison.OrdinalIgnoreCase));
            if (next is null) return false;
            node = next;
        }
        return node.HasSensor;
    }

    // ------------------------------------------------------ whole-tree validation

    /// <summary>
    /// Validates an entire hierarchy with an explicit stack instead of recursion, so a deep tree can
    /// never overflow the call stack. Collects every problem rather than stopping at the first.
    /// </summary>
    public TreeValidationResult ValidateStructure(DeploymentNode root)
    {
        var errors = new List<string>();
        int nodes = 0, sensors = 0, maxDepth = 0;

        var pending = new Stack<(DeploymentNode Node, int Depth, string Path)>();
        pending.Push((root, 1, root.Name));

        while (pending.Count > 0)
        {
            var (node, depth, path) = pending.Pop();
            nodes++;
            maxDepth = Math.Max(maxDepth, depth);
            if (node.HasSensor) sensors++;

            if (string.IsNullOrWhiteSpace(node.Name))
                errors.Add($"Node at depth {depth} has an empty name.");

            if (depth >= MaxDepth && node.Children.Count > 0)
            {
                errors.Add($"'{path}' goes deeper than the supported {MaxDepth} levels.");
                continue;
            }

            foreach (var dup in node.Children.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                errors.Add($"'{path}' has more than one child named '{dup.Key}'.");

            foreach (var child in node.Children)
                pending.Push((child, depth + 1, $"{path}/{child.Name}"));
        }

        return new TreeValidationResult(errors.Count == 0, nodes, maxDepth, sensors, errors);
    }

    public int CountNodes(DeploymentNode node) => 1 + node.Children.Sum(CountNodes);

    public DeploymentNode BuildDemoTree() => new()
    {
        Name = "Facility A",
        Children = new()
        {
            new DeploymentNode
            {
                Name = "Zone 1",
                Children = new()
                {
                    new DeploymentNode
                    {
                        Name = "Sub-Zone B",
                        Children = new() { new DeploymentNode { Name = "Rack 1", HasSensor = true } }
                    },
                    new DeploymentNode { Name = "Sub-Zone C", HasSensor = false }
                }
            }
        }
    };
}