using SmartX.Api.Domain.Entities;

namespace SmartX.Api.Services;

public class TelemetryTimelineService
{
    private readonly SortedDictionary<DateTime, List<TelemetryLog>> _timeline = new();
    private readonly object _lock = new();
    private const int MaxBuckets = 200; // cap memory; oldest buckets drop off

    public void Record(TelemetryLog log)
    {
        var bucket = new DateTime(log.Timestamp.Year, log.Timestamp.Month, log.Timestamp.Day,
            log.Timestamp.Hour, log.Timestamp.Minute, log.Timestamp.Second);

        lock (_lock)
        {
            if (!_timeline.TryGetValue(bucket, out var list))
            {
                list = new List<TelemetryLog>();
                _timeline[bucket] = list;
            }
            list.Add(log);

            while (_timeline.Count > MaxBuckets)
            {
                var oldestKey = _timeline.Keys.First(); // SortedDictionary keeps keys in order, so this is genuinely the oldest
                _timeline.Remove(oldestKey);
            }
        }
    }

    public List<(DateTime Bucket, List<TelemetryLog> Readings)> GetChronological(int take = 50)
    {
        lock (_lock)
            return _timeline
                .OrderByDescending(kv => kv.Key) // most recent first for display
                .Take(take)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();
    }

    public int BucketCount { get { lock (_lock) return _timeline.Count; } }
}