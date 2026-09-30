using SmartX.Api.Domain.Models;
using SmartX.Api.Dtos;
using SmartX.Api.Hubs;
using SmartX.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace SmartX.Api.Endpoints;

public static class CommandStreamEndpoints
{
    public static void MapCommandStreamEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/commands").WithTags("CommandStream");

        group.MapGet("/queues", (CommandStreamService cmds) => Results.Ok(new
        {
            standardDepth = cmds.StandardQueueDepth,
            priorityDepth = cmds.PriorityQueueDepth
        }));

        group.MapGet("/history", (CommandStreamService cmds) => Results.Ok(cmds.GetHistorySnapshot()));

        group.MapPost("/issue", async (IssueCommandRequest req, CommandStreamService cmds, IHubContext<TelemetryHub> hub) =>
        {
            if (!Enum.TryParse<CommandType>(req.CommandType, out var type))
                throw new ArgumentException($"Unknown command type '{req.CommandType}'.");

            var command = new DeviceCommand { TargetMac = req.TargetMac, Type = type, IsPriority = true };
            cmds.PushCommand(command);

            await hub.Clients.All.SendAsync("CommandIssued", command);
            return Results.Ok(command);
        });

        group.MapPost("/undo", async (CommandStreamService cmds, IHubContext<TelemetryHub> hub) =>
        {
            var undone = cmds.PopLastCommand();
            if (undone is null) return Results.NotFound(new { message = "No commands to undo." });

            await hub.Clients.All.SendAsync("CommandUndone", undone);
            return Results.Ok(undone);
        });

        group.MapGet("/timeline", (TelemetryTimelineService timeline) =>
        Results.Ok(timeline.GetChronological().Select(b => new
        {
            bucket = b.Bucket,
            count = b.Readings.Count,
            sensors = b.Readings.Select(r => new { r.SensorId, r.RawValue, r.Unit }).ToList()
        })));

        group.MapGet("/faults", (ActiveFaultTracker faults) => Results.Ok(faults.GetAllFaulted()));
    }
}