using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public static class MeshEndpoints
{
    public static void MapMeshEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/mesh").WithTags("Mesh");

        // Nodes with live status, plus every link and whether both of its ends are up.
        group.MapGet("/topology", (MeshService mesh) =>
        {
            if (!mesh.IsReady)
                return Results.Json(new { message = "The mesh topology is still loading. Try again in a moment." }, statusCode: 503);

            return Results.Ok(mesh.GetSnapshot());
        });
    }
}