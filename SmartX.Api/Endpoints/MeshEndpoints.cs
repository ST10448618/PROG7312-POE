using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public static class MeshEndpoints
{
    private static IResult Loading() =>
        Results.Json(new { message = "The mesh topology is still loading. Try again in a moment." }, statusCode: 503);

    private static List<string> ParseIds(string? raw) =>
        (raw ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public static void MapMeshEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/mesh").WithTags("Mesh");

        // Nodes with live status, plus every link and whether both of its ends are up.
        group.MapGet("/topology", (MeshService mesh) =>
            mesh.IsReady ? Results.Ok(mesh.GetSnapshot()) : Loading());

        // MST, subnets (DFS), reachability and reroutes. ?assumeDown=mac1,mac2 layers a what-if on top of live state.
        group.MapGet("/analysis", (MeshService mesh, MeshAnalysisService analysis, string? assumeDown) =>
        {
            if (!mesh.IsReady) return Loading();

            var ids = ParseIds(assumeDown);
            var unknown = analysis.UnknownIds(ids);
            if (unknown.Count > 0)
                return Results.BadRequest(new { message = $"Unknown node(s): {string.Join(", ", unknown)}" });

            return Results.Ok(analysis.Analyze(ids));
        });

        // Route for one node to the gateway, by MAC: the MST route and the fewest-hops (BFS) route.
        group.MapGet("/route/{mac}", (string mac, MeshService mesh, MeshAnalysisService analysis, string? assumeDown) =>
        {
            if (!mesh.IsReady) return Loading();

            var ids = ParseIds(assumeDown);
            var unknown = analysis.UnknownIds(ids);
            if (unknown.Count > 0)
                return Results.BadRequest(new { message = $"Unknown node(s): {string.Join(", ", unknown)}" });

            var report = analysis.Route(mac, ids);
            return report is null
                ? Results.NotFound(new { message = $"No node with MAC '{mac}' is in the mesh." })
                : Results.Ok(report);
        });
    }
}