using System.Collections.Concurrent;
using SmartX.Api.Domain.Models;

namespace SmartX.Api.Services;

public class LiveDeviceRegistry
{
    private readonly ConcurrentDictionary<string, LiveDeviceState> _devices = new();

    public void Register(string mac, string location, string category)
    {
        // Upsert rather than replace, so re-registering after an edit does not wipe actuator state.
        _devices.AddOrUpdate(
            mac,
            _ => new LiveDeviceState
            {
                MacAddress = mac,
                Location = location,
                Category = category,
                Status = "Online",
                LastSeen = DateTime.UtcNow
            },
            (_, existing) =>
            {
                existing.Location = location;
                existing.Category = category;
                existing.Status = "Online";
                existing.LastSeen = DateTime.UtcNow;
                return existing;
            });
    }

    public void Remove(string mac) => _devices.TryRemove(mac, out _);

    public void UpdateReading(string mac, double value, string status, string? location = null, string? category = null)
    {
        _devices.AddOrUpdate(
            mac,
            addValueFactory: _ => new LiveDeviceState
            {
                MacAddress = mac,
                Location = location ?? "Unknown",
                Category = category ?? "Unknown",
                Status = status,
                LastValue = value,
                LastSeen = DateTime.UtcNow
            },
            updateValueFactory: (_, existing) =>
            {
                existing.LastValue = value;
                existing.Status = status;
                existing.LastSeen = DateTime.UtcNow;
                if (location is not null) existing.Location = location;
                if (category is not null) existing.Category = category;
                return existing;
            });
    }

    public LiveDeviceState? Get(string mac) =>
        _devices.TryGetValue(mac, out var device) ? device : null;

    public List<LiveDeviceState> GetAll() => _devices.Values.ToList();

    public int Count => _devices.Count;

    public (ActuatorState Before, ActuatorState After) ApplyCommand(string mac, CommandType type)
    {
        var device = Get(mac) ?? throw new KeyNotFoundException($"Device '{mac}' is not in the live registry.");
        lock (device)
        {
            var before = device.Actuators;
            device.Actuators = before.Apply(type);
            return (before, device.Actuators);
        }
    }

    public bool RestoreActuators(string mac, ActuatorState state)
    {
        var device = Get(mac);
        if (device is null) return false;
        lock (device) device.Actuators = state;
        return true;
    }
}