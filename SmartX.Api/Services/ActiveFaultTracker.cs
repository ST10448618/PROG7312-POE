namespace SmartX.Api.Services;

public class ActiveFaultTracker
{
    private readonly HashSet<string> _faultedDevices = new();
    private readonly object _lock = new();

    /// <returns>true if this was a genuinely new fault (not already tracked), false if it was a duplicate.</returns>
    public bool MarkFaulted(string mac)
    {
        lock (_lock) return _faultedDevices.Add(mac); // HashSet<T>.Add returns false if already present
    }

    public bool ClearFault(string mac)
    {
        lock (_lock) return _faultedDevices.Remove(mac);
    }

    public bool IsFaulted(string mac)
    {
        lock (_lock) return _faultedDevices.Contains(mac);
    }

    public List<string> GetAllFaulted()
    {
        lock (_lock) return _faultedDevices.ToList();
    }

    public int Count { get { lock (_lock) return _faultedDevices.Count; } }
}