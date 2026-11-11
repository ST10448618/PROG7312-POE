using System.Collections.Concurrent;
using System.Diagnostics;
using SmartX.Api.Domain.Collections;
using SmartX.Api.Domain.Models;

namespace SmartX.Api.Services;

public record HistoryQueryResult(
    string SensorId, DateTime From, DateTime To, bool Descending,
    int Total, double? Min, double? Max, double? Average,
    int Skip, int Take, List<TelemetryPoint> Points,
    double ElapsedMicroseconds, int TreeSize, int TreeHeight);

public record HistorySummary(
    string SensorId, int Count, DateTime? First, DateTime? Last,
    int TreeHeight, int MinimumPossibleHeight, bool IsBalanced, string? Problem);

public class TelemetryHistoryService
{
    /// <summary>Oldest readings are dropped beyond this, so memory use stays bounded on a long-running gateway.</summary>
    public const int MaxPointsPerSensor = 100_000;

    private sealed class SensorHistory
    {
        public readonly AvlTree<DateTime, TelemetryPoint> Tree = new();
        public readonly object Lock = new();
    }

    private readonly ConcurrentDictionary<string, SensorHistory> _sensors = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _ready;
    private DateTime _warmUpCutoff = DateTime.MinValue;

    /// <summary>True once the trees have been rebuilt from the database.</summary>
    public bool IsReady => _ready;

    public IReadOnlyCollection<string> SensorIds => _sensors.Keys.ToList();

    public int TotalPoints => _sensors.Values.Sum(h => { lock (h.Lock) return h.Tree.Count; });

    // ------------------------------------------------------------- warm-up

    public void BeginWarmUp(DateTime cutoffUtc) => _warmUpCutoff = cutoffUtc;

    public void CompleteWarmUp() => _ready = true;

    // -------------------------------------------------------------- writes

    /// <summary>A reading that has just arrived. While the database load is still running, anything older than the cutoff is skipped because the load already covers it.</summary>
    public void Add(string sensorId, TelemetryPoint point)
    {
        if (!_ready && point.Timestamp < _warmUpCutoff) return;
        Insert(sensorId, point);
    }

    /// <summary>A reading read back from the database during warm-up.</summary>
    public void AddHistorical(string sensorId, TelemetryPoint point) => Insert(sensorId, point);

    private void Insert(string sensorId, TelemetryPoint point)
    {
        var history = _sensors.GetOrAdd(sensorId, _ => new SensorHistory());
        lock (history.Lock)
        {
            history.Tree.Add(point.Timestamp, point);
            while (history.Tree.Count > MaxPointsPerSensor)
                history.Tree.TryRemoveMin(out _, out _);
        }
    }

    // --------------------------------------------------------------- reads

    public HistoryQueryResult? Query(string sensorId, DateTime from, DateTime to, int skip = 0, int take = 100, bool descending = false)
    {
        if (!_sensors.TryGetValue(sensorId, out var history)) return null;

        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 1000);

        var page = new List<TelemetryPoint>(Math.Min(take, 256));
        var total = 0;
        double min = double.MaxValue, max = double.MinValue, sum = 0;
        int treeSize, treeHeight;

        var started = Stopwatch.GetTimestamp();
        lock (history.Lock)
        {
            treeSize = history.Tree.Count;
            treeHeight = history.Tree.Height;

            var range = descending ? history.Tree.RangeDescending(from, to) : history.Tree.Range(from, to);
            foreach (var entry in range)
            {
                var p = entry.Value;
                if (total >= skip && page.Count < take) page.Add(p);
                total++;
                sum += p.Value;
                if (p.Value < min) min = p.Value;
                if (p.Value > max) max = p.Value;
            }
        }
        var elapsedMicros = Stopwatch.GetElapsedTime(started).TotalMilliseconds * 1000.0;

        return new HistoryQueryResult(sensorId, from, to, descending, total,
            total == 0 ? null : min, total == 0 ? null : max, total == 0 ? null : Math.Round(sum / total, 3),
            skip, take, page, Math.Round(elapsedMicros, 1), treeSize, treeHeight);
    }

    public HistorySummary? Summarise(string sensorId)
    {
        if (!_sensors.TryGetValue(sensorId, out var history)) return null;

        lock (history.Lock)
        {
            var tree = history.Tree;
            DateTime? first = tree.TryGetMin(out var minKey, out _) ? minKey : null;
            DateTime? last = tree.TryGetMax(out var maxKey, out _) ? maxKey : null;
            var balanced = tree.IsValid(out var problem);
            return new HistorySummary(sensorId, tree.Count, first, last, tree.Height, tree.MinimumPossibleHeight, balanced, problem);
        }
    }
}