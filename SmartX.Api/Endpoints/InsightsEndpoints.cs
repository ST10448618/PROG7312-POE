using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Dtos;
using SmartX.Api.Hubs;
using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public static class InsightsEndpoints
{
    public static void MapInsightsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/insights").WithTags("Insights");

        group.MapGet("/", (PredictiveActionEngine engine) => Results.Ok(engine.Snapshot()));

        group.MapPost("/search", async (SearchActivityRequest req, PredictiveActionEngine engine, IHubContext<TelemetryHub> hub) =>
        {
            engine.RecordSearch(req.Text, req.Category, req.State);
            await hub.Clients.All.SendAsync("InsightsUpdated", engine.Snapshot());
            return Results.Ok();
        });
    }
}