using Microsoft.EntityFrameworkCore;
using SmartX.Api.Domain.Entities;
using SmartX.Api.Infrastructure.Data;

namespace SmartX.Api.Services;

public class SensorService
{
    private readonly AppDbContext _db;
    private readonly LiveDeviceRegistry _registry;
    public SensorService(AppDbContext db, LiveDeviceRegistry registry)
    {
        _db = db;
        _registry = registry;
    }

    public async Task<SensorProfile> RegisterAsync(string mac, string location, string category)
    {
        if (string.IsNullOrWhiteSpace(mac))
            throw new ArgumentException("MAC address is required.");

        if (await _db.Sensors.AnyAsync(s => s.MacAddress == mac))
            throw new InvalidOperationException($"Sensor '{mac}' is already registered.");

        var sensor = new SensorProfile { MacAddress = mac, Location = location, Category = category };
        _db.Sensors.Add(sensor);
        await _db.SaveChangesAsync();

        _registry.Register(mac, location, category);
        return sensor;
    }

    public Task<List<SensorProfile>> GetAllAsync() =>
        _db.Sensors.Include(s => s.Files).AsNoTracking().ToListAsync();

    public async Task<SensorProfile> UpdateStatusAsync(string mac, string status)
    {
        var sensor = await _db.Sensors.FirstOrDefaultAsync(s => s.MacAddress == mac)
            ?? throw new KeyNotFoundException($"Sensor '{mac}' not found.");
        sensor.Status = status;
        await _db.SaveChangesAsync();

        _registry.UpdateReading(mac, _registry.Get(mac)?.LastValue ?? 0, status);
        return sensor;
    }

    public async Task<object> GetSummaryAsync()
{
    var sensors = await _db.Sensors.AsNoTracking().ToListAsync();
    var telemetryCount = await _db.TelemetryLogs.CountAsync();

    return new
    {
        totalSensors = sensors.Count,
        onlineSensors = sensors.Count(s => s.Status == "Online"),
        totalTelemetryLogs = telemetryCount,
        byCategory = sensors.GroupBy(s => s.Category).ToDictionary(g => g.Key, g => g.Count())
    };
}

public async Task<SensorProfile> UpdateAsync(string mac, string location, string category)
{
    var sensor = await _db.Sensors.FirstOrDefaultAsync(s => s.MacAddress == mac)
        ?? throw new KeyNotFoundException($"Sensor '{mac}' not found.");
    sensor.Location = location;
    sensor.Category = category;
    await _db.SaveChangesAsync();
    return sensor;
}

public async Task DeleteAsync(string mac)
{
    var sensor = await _db.Sensors.FirstOrDefaultAsync(s => s.MacAddress == mac)
        ?? throw new KeyNotFoundException($"Sensor '{mac}' not found.");
    _db.Sensors.Remove(sensor);
    await _db.SaveChangesAsync();
}
}