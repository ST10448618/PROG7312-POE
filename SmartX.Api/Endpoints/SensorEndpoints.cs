using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Domain.Entities;
using SmartX.Api.Domain.ValueObjects;
using SmartX.Api.Dtos;
using SmartX.Api.Hubs;
using SmartX.Api.Infrastructure.Data;
using SmartX.Api.Infrastructure.FileStorage;
using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public static class SensorEndpoints
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    public static void MapSensorEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sensors").WithTags("Sensors");

        group.MapPost("/", async (RegisterSensorRequest req, SensorService sensors, IHubContext<TelemetryHub> hub) =>
        {
            var sensor = await sensors.RegisterAsync(req.MacAddress, req.Location, req.Category);
            await hub.Clients.All.SendAsync("SensorRegistered", sensor.MacAddress);
            return Results.Created($"/api/sensors/{sensor.Id}", sensor);
        });

        group.MapGet("/registry", (LiveDeviceRegistry registry) => Results.Ok(registry.GetAll()));

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

        // Optional ?category=Config|Photo|Log|Other narrows the list to one class of file.
        group.MapGet("/{mac}/files", async (string mac, string? category, AppDbContext db) =>
        {
            var query = db.SensorFiles.AsNoTracking().Where(f => f.SensorProfile!.MacAddress == mac);

            if (!string.IsNullOrWhiteSpace(category))
            {
                if (!FileCategory.IsValid(category))
                    throw new ArgumentException($"Unknown category '{category}'. Use Config, Photo, Log or Other.");

                var wanted = FileCategory.Normalise(category);
                query = wanted == FileCategory.Other
                    ? query.Where(f => f.Category == FileCategory.Other || f.Category == "") // rows saved before classification existed
                    : query.Where(f => f.Category == wanted);
            }

            var files = await query
                .OrderByDescending(f => f.UploadedAt)
                .ThenByDescending(f => f.Id)
                .Select(f => new
                {
                    f.Id,
                    f.FileName,
                    Category = f.Category == "" ? FileCategory.Other : f.Category,
                    f.ContentType,
                    f.SizeBytes,
                    f.UploadedAt
                })
                .ToListAsync();

            return Results.Ok(files);
        });

        // Optional ?category=...; when omitted the class is inferred from the file extension.
        group.MapPost("/{mac}/upload", async (string mac, IFormFile file, string? category, AppDbContext db, IFileStorageService storage) =>
        {
            if (file is null || file.Length == 0)
                throw new ArgumentException("Empty file.");
            if (file.Length > MaxUploadBytes)
                throw new ArgumentException($"File is too large (limit {MaxUploadBytes / (1024 * 1024)} MB).");
            if (!string.IsNullOrWhiteSpace(category) && !FileCategory.IsValid(category))
                throw new ArgumentException($"Unknown category '{category}'. Use Config, Photo, Log or Other.");

            var sensor = await db.Sensors.FirstOrDefaultAsync(s => s.MacAddress == mac)
                ?? throw new KeyNotFoundException($"Sensor '{mac}' not found.");

            var resolved = FileCategory.Classify(file.FileName, category);

            await using var stream = file.OpenReadStream();
            var savedPath = await storage.SaveEncryptedAsync(mac, file.FileName, stream);

            db.SensorFiles.Add(new SensorFile
            {
                FileName = file.FileName,
                StoredPath = savedPath,
                Category = resolved,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                SizeBytes = file.Length,
                UploadedAt = DateTime.UtcNow,
                SensorProfileId = sensor.Id
            });
            await db.SaveChangesAsync();

            return Results.Ok(new { savedTo = savedPath, encrypted = true, category = resolved, sizeBytes = file.Length });
        }).DisableAntiforgery();

        group.MapGet("/{mac}/files/{fileId}/download", async (string mac, int fileId, AppDbContext db, IFileStorageService storage) =>
        {
            var file = await db.SensorFiles
                .Include(f => f.SensorProfile)
                .FirstOrDefaultAsync(f => f.Id == fileId && f.SensorProfile!.MacAddress == mac)
                ?? throw new KeyNotFoundException("File not found.");

            var stream = storage.OpenDecryptedStream(file.StoredPath);
            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
            return Results.File(stream, contentType, file.FileName);
        });
    }
}