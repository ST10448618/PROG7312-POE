using System.Collections.Concurrent;
using SmartX.Api.Domain.Models;

namespace SmartX.Api.Services;

public class LiveDeviceRegistry
{
    private readonly ConcurrentDictionary<string, LiveDeviceState> _devices = new();

    public void Register(string mac, string location, string category)
    {
        _devices[mac] = new LiveDeviceState
        {
            MacAddress = mac,
            Location = location,
            Category = category,
            Status = "Online",
            LastSeen = DateTime.UtcNow
        };
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
}