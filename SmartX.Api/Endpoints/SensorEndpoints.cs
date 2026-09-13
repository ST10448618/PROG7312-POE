using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Domain.Entities;
using SmartX.Api.Dtos;
using SmartX.Api.Hubs;
using SmartX.Api.Infrastructure.Data;
using SmartX.Api.Infrastructure.FileStorage;
using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public static class SensorEndpoints
{
    public static void MapSensorEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sensors").WithTags("Sensors");

        group.MapPost("/", async (RegisterSensorRequest req, SensorService sensors, IHubContext<TelemetryHub> hub) =>
        {
            var sensor = await sensors.RegisterAsync(req.MacAddress, req.Location, req.Category);
            await hub.Clients.All.SendAsync("SensorRegistered", sensor.MacAddress);
            return Results.Created($"/api/sensors/{sensor.Id}", sensor);
        });

        group.MapGet("/", async (SensorService sensors) => Results.Ok(await sensors.GetAllAsync()));

        group.MapPost("/{mac}/status", async (string mac, SensorStatusRequest req, SensorService sensors) =>
            Results.Ok(await sensors.UpdateStatusAsync(mac, req.Status)));

        group.MapGet("/summary", async (SensorService sensors) => Results.Ok(await sensors.GetSummaryAsync()));

        group.MapPut("/{mac}", async (string mac, UpdateSensorRequest req, SensorService sensors) =>
        Results.Ok(await sensors.UpdateAsync(mac, req.Location, req.Category)));

        group.MapDelete("/{mac}", async (string mac, SensorService sensors) =>
        {
            await sensors.DeleteAsync(mac);
            return Results.NoContent();
        });

        group.MapPost("/{mac}/upload", async (string mac, IFormFile file, AppDbContext db, IFileStorageService storage) =>
        {
            if (file is null || file.Length == 0)
                throw new ArgumentException("Empty file.");

            var sensor = await db.Sensors.FirstOrDefaultAsync(s => s.MacAddress == mac)
                ?? throw new KeyNotFoundException($"Sensor '{mac}' not found.");

            await using var stream = file.OpenReadStream();
            var savedPath = await storage.SaveEncryptedAsync(mac, file.FileName, stream);

            db.SensorFiles.Add(new SensorFile
            {
                FileName = file.FileName,
                StoredPath = savedPath,
                SensorProfileId = sensor.Id
            });
            await db.SaveChangesAsync();

            return Results.Ok(new { savedTo = savedPath, encrypted = true });
        }).DisableAntiforgery();
    }
}