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

    public void UpdateReading(string mac, double value, string status)
    {
        if (_devices.TryGetValue(mac, out var device))
        {
            device.LastValue = value;
            device.Status = status;
            device.LastSeen = DateTime.UtcNow;
        }
    }

    public LiveDeviceState? Get(string mac) =>
        _devices.TryGetValue(mac, out var device) ? device : null;

    public List<LiveDeviceState> GetAll() => _devices.Values.ToList();

    public int Count => _devices.Count;
}