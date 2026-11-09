using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Domain.Models;
using SmartX.Api.Dtos;
using SmartX.Api.Hubs;
using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public static class CommandStreamEndpoints
{
    public static void MapCommandStreamEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/commands").WithTags("CommandStream");

        group.MapGet("/queues", (CommandStreamService cmds) => Results.Ok(new
        {
            standardDepth = cmds.StandardQueueDepth,
            priorityDepth = cmds.PriorityQueueDepth,
            processedStandard = cmds.ProcessedStandardTotal,
            processedPriority = cmds.ProcessedPriorityTotal
        }));

        group.MapGet("/processed", (CommandStreamService cmds, int? take) =>
            Results.Ok(cmds.GetProcessedLog(Math.Clamp(take ?? 20, 1, 50))));

        group.MapGet("/history", (CommandStreamService cmds) => Results.Ok(cmds.GetHistorySnapshot()));

        group.MapGet("/timeline", (TelemetryTimelineService timeline) =>
            Results.Ok(timeline.GetChronological().Select(b => new
            {
                bucket = b.Bucket,
                count = b.Readings.Count,
                sensors = b.Readings.Select(r => new { r.SensorId, r.RawValue, r.Unit }).ToList()
            })));

        group.MapGet("/faults", (ActiveFaultTracker faults) => Results.Ok(faults.GetAllFaulted()));

        group.MapPost("/issue", async (IssueCommandRequest req, CommandStreamService cmds, LiveDeviceRegistry registry,
            PredictiveActionEngine engine, IHubContext<TelemetryHub> hub) =>
        {
            if (!Enum.TryParse<CommandType>(req.CommandType, out var type) || !Enum.IsDefined(type))
                throw new ArgumentException($"Unknown command type '{req.CommandType}'.");

            var command = cmds.IssueCommand(registry, req.TargetMac, type); // 404 if the device is unknown
            engine.RecordUserAction($"Command:{type}:{req.TargetMac}");

            await hub.Clients.All.SendAsync("CommandIssued", command);
            await hub.Clients.All.SendAsync("DeviceRegistryUpdated", registry.GetAll());
            await hub.Clients.All.SendAsync("InsightsUpdated", engine.Snapshot());
            return Results.Ok(command);
        });

        group.MapPost("/undo", async (CommandStreamService cmds, LiveDeviceRegistry registry, IHubContext<TelemetryHub> hub) =>
        {
            var undone = cmds.UndoLastCommand(registry);
            if (undone is null) return Results.NotFound(new { message = "No commands to undo." });

            await hub.Clients.All.SendAsync("CommandUndone", undone);
            await hub.Clients.All.SendAsync("DeviceRegistryUpdated", registry.GetAll());
            return Results.Ok(undone);
        });
    }
}