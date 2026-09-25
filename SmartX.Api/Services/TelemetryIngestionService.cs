using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Domain.Collections;
using SmartX.Api.Domain.Entities;
using SmartX.Api.Domain.ValueObjects;
using SmartX.Api.Hubs;
using SmartX.Api.Infrastructure.Data;

namespace SmartX.Api.Services;

public class TelemetryIngestionService
{
    private readonly AppDbContext _db;
    private readonly AnomalyDetectionService _anomaly;
    private readonly TelemetryBatchStore _batches;
    private readonly IHubContext<TelemetryHub> _hub;
    private readonly IntegrationDispatchService _dispatch;
    private readonly LiveDeviceRegistry _registry;

    public TelemetryIngestionService(
        AppDbContext db, AnomalyDetectionService anomaly, TelemetryBatchStore batches,
        IHubContext<TelemetryHub> hub, IntegrationDispatchService dispatch, LiveDeviceRegistry registry)
    {
        _db = db;
        _anomaly = anomaly;
        _batches = batches;
        _hub = hub;
        _dispatch = dispatch;
        _registry = registry;
    }

    public async Task<AnomalyResult> IngestAsync<T>(TelemetryPacket<T> packet) where T : struct
    {
        _db.TelemetryLogs.Add(new TelemetryLog
        {
            SensorId = packet.SensorId,
            ValueType = typeof(T).Name,
            RawValue = packet.Value.ToString() ?? "",
            Unit = packet.Unit,
            Timestamp = packet.Timestamp
        });

        _batches.Append(packet.SensorId, packet.NumericValue, packet.Timestamp);
        var result = _anomaly.Score(packet.SensorId, packet.NumericValue);
        var severity = AnomalyDetectionService.SeverityFor(result.Colour);

        _db.AnomalyLogs.Add(new AnomalyLog
        {
            SensorId = packet.SensorId,
            Value = result.Value,
            Score = result.Score,
            Colour = result.Colour.ToString(),
            Severity = severity,
            Timestamp = result.Timestamp
        });

        await _db.SaveChangesAsync();

        _registry.UpdateReading(packet.SensorId, packet.NumericValue, severity == "Disconnected" ? "Disconnected" : "Online");

        await _hub.Clients.All.SendAsync("AnomalyUpdate", result);
        await _hub.Clients.All.SendAsync("TelemetryIngested", new { packet.SensorId, packet.Timestamp, Value = packet.NumericValue });
        await _hub.Clients.All.SendAsync("DeviceRegistryUpdated", _registry.GetAll());

        if (severity == "Critical")
            await _dispatch.NotifyAllAsync(packet.SensorId, result.Value, result.Score);

        return result;
    }

    public async Task<AnomalyResult> MarkDisconnectedAsync(string sensorId)
    {
        var result = _anomaly.Score(sensorId, 0, sensorConnected: false);
        var severity = AnomalyDetectionService.SeverityFor(result.Colour);

        _db.AnomalyLogs.Add(new AnomalyLog
        {
            SensorId = sensorId,
            Value = 0,
            Score = 0,
            Colour = result.Colour.ToString(),
            Severity = severity,
            Timestamp = result.Timestamp
        });
        await _db.SaveChangesAsync();

        _registry.UpdateReading(sensorId, 0, "Disconnected");

        await _hub.Clients.All.SendAsync("AnomalyUpdate", result);
        await _hub.Clients.All.SendAsync("DeviceRegistryUpdated", _registry.GetAll());
        return result;
    }
}
