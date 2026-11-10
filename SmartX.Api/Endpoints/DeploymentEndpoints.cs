using System.Text.Json;
using SmartX.Api.Dtos;
using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public static class DeploymentEndpoints
{
    public static void MapDeploymentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/deployment").WithTags("Deployment");

        group.MapPost("/validate", (DeploymentPathRequest req, DeploymentTreeService trees) =>
        {
            var demoTree = trees.BuildDemoTree();
            var segments = req.Path.Split('/', StringSplitOptions.TrimEntries);
            var isValid = segments[0].Equals(demoTree.Name, StringComparison.OrdinalIgnoreCase)
                          && trees.ValidatePath(demoTree, segments);
            return Results.Ok(new { path = req.Path, isValid });
        });

        // Validates a whole nested tree posted as JSON: {"name": "...", "hasSensor": false, "children": [ ... ]}.
        // The body is read manually so the JSON depth limit can be raised to match the supported tree depth.
        group.MapPost("/validate-tree", async (HttpRequest request, DeploymentTreeService trees) =>
        {
            JsonDocument doc;
            try
            {
                doc = await JsonDocument.ParseAsync(request.Body,
                    new JsonDocumentOptions { MaxDepth = DeploymentTreeService.MaxJsonDepth });
            }
            catch (JsonException ex)
            {
                throw new ArgumentException(
                    $"Body must be valid JSON nested no deeper than {DeploymentTreeService.MaxDepth} tree levels. {ex.Message}");
            }

            using (doc)
            {
                var root = trees.ParseFromJson(doc.RootElement);
                return Results.Ok(trees.ValidateStructure(root));
            }
        });

        group.MapGet("/demo-tree/node-count", (DeploymentTreeService trees) =>
            Results.Ok(new { nodeCount = trees.CountNodes(trees.BuildDemoTree()) }));
    }
}