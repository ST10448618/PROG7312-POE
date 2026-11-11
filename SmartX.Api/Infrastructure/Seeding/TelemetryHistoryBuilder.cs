using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Domain.Entities;
using SmartX.Api.Domain.Models;
using SmartX.Api.Infrastructure.Data;
using SmartX.Api.Services;

namespace SmartX.Api.Infrastructure.Seeding;

public class TelemetryHistoryBuilder : BackgroundService
{
    private const int BatchSize = 5_000;

    private readonly IServiceProvider _services;
    private readonly TelemetryHistoryService _history;
    private readonly IConfiguration _config;
    private readonly ILogger<TelemetryHistoryBuilder> _logger;

    public TelemetryHistoryBuilder(IServiceProvider services, TelemetryHistoryService history,
        IConfiguration config, ILogger<TelemetryHistoryBuilder> logger)
    {
        _services = services;
        _history = history;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var timer = Stopwatch.StartNew();
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var cutoff = DateTime.UtcNow;
            _history.BeginWarmUp(cutoff);

            var seeded = await SeedAsync(db, cutoff, stoppingToken);
            var seedMs = timer.ElapsedMilliseconds;

            var loaded = await LoadAsync(db, cutoff, stoppingToken);

            _logger.LogInformation(
                "Telemetry history ready: {Seeded} readings back-filled ({SeedMs} ms), {Loaded} loaded into AVL trees ({TotalMs} ms total).",
                seeded, seedMs, loaded, timer.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Building telemetry history failed. History queries will only cover readings received from now on.");
        }
        finally
        {
            _history.CompleteWarmUp();
        }
    }

    private async Task<int> SeedAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        var days = _config.GetValue("History:SeedDays", 7);
        var intervalSeconds = Math.Max(10, _config.GetValue("History:SeedIntervalSeconds", 75));
        if (days <= 0) return 0;

        var wanted = (int)(TimeSpan.FromDays(days).TotalSeconds / intervalSeconds);
        var existing = await db.TelemetryLogs.AsNoTracking()
            .GroupBy(t => t.SensorId)
            .Select(g => new { Sensor = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Sensor, x => x.Count, ct);

        db.ChangeTracker.AutoDetectChangesEnabled = false; // bulk insert: no need to scan tracked entities after every Add
        var batch = new List<TelemetryLog>(BatchSize);
        var inserted = 0;

        foreach (var sensor in MeshTopologyDefinition.Sensors)
        {
            // Already has a decent history (a restart, or a database that kept its volume): leave it alone.
            if (existing.TryGetValue(sensor.Mac, out var have) && have >= wanted / 2) continue;

            foreach (var log in TelemetryHistoryGenerator.Generate(sensor, now, days, intervalSeconds))
            {
                batch.Add(log);
                if (batch.Count < BatchSize) continue;

                inserted += await FlushAsync(db, batch, ct);
            }
        }

        if (batch.Count > 0) inserted += await FlushAsync(db, batch, ct);
        return inserted;
    }

    private static async Task<int> FlushAsync(AppDbContext db, List<TelemetryLog> batch, CancellationToken ct)
    {
        var count = batch.Count;
        db.TelemetryLogs.AddRange(batch);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        batch.Clear();
        return count;
    }

    private async Task<int> LoadAsync(AppDbContext db, DateTime cutoff, CancellationToken ct)
    {
        var loaded = 0;

        // Oldest first: sorted input is the worst case for a plain BST, and exactly what the AVL rotations are for.
        var rows = db.TelemetryLogs.AsNoTracking()
            .Where(t => t.Timestamp < cutoff)
            .OrderBy(t => t.Timestamp)
            .AsAsyncEnumerable();

        await foreach (var row in rows.WithCancellation(ct))
        {
            _history.AddHistorical(row.SensorId, new TelemetryPoint(row.Timestamp, ToNumber(row.RawValue), row.Unit, row.ValueType));
            loaded++;
        }

        return loaded;
    }

    private static double ToNumber(string raw)
    {
        if (bool.TryParse(raw, out var flag)) return flag ? 1 : 0;
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;
        return double.TryParse(raw.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : 0;
    }
}