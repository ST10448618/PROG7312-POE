using System.Diagnostics;
using SmartX.Api.Domain.Collections;
using SmartX.Api.Domain.Models;

namespace SmartX.Api.Services;

public record BenchmarkRow(
    int Size, int Queries, int WindowPoints,
    double AvlBuildMs, double LinearScanMicros, double AvlRangeMicros, double Speedup,
    int AvlHeight, int MinimumPossibleHeight, int? UnbalancedBstHeight, bool ResultsMatch);

public record BenchmarkReport(List<BenchmarkRow> Rows, string Method);
public static class HistoryBenchmark
{
    public const int MaxSize = 500_000;
    private const int UnbalancedDemoLimit = 5_000; // building an unbalanced tree from sorted input is O(n²)

    public static BenchmarkReport Run(IEnumerable<int> sizes, int queries)
    {
        queries = Math.Clamp(queries, 10, 2000);
        var sizeList = sizes.Select(s => Math.Clamp(s, 100, MaxSize)).Distinct().Take(6).ToList();

        // Warm the JIT so the first measured size is not penalised.
        MeasureOne(1_000, 50, new Random(1));

        var rnd = new Random(42);
        var rows = sizeList.Select(size => MeasureOne(size, queries, rnd)).ToList();

        return new BenchmarkReport(rows,
            $"One reading per minute, sorted by time. Each query asks for a 2-hour window at a random position " +
            $"({queries} queries per size). Linear scan tests every reading in a List; the AVL tree walks to the " +
            $"start of the window and reads forward. Times are the average per query.");
    }

    private static BenchmarkRow MeasureOne(int size, int queries, Random rnd)
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var data = new List<TelemetryPoint>(size);
        for (var i = 0; i < size; i++)
            data.Add(new TelemetryPoint(start.AddMinutes(i), Math.Round(rnd.NextDouble() * 100, 2), "%", "Single"));

        var build = Stopwatch.GetTimestamp();
        var tree = new AvlTree<DateTime, TelemetryPoint>();
        foreach (var p in data) tree.Add(p.Timestamp, p);
        var buildMs = Stopwatch.GetElapsedTime(build).TotalMilliseconds;

        var windowPoints = Math.Min(120, Math.Max(1, size / 10));
        var windows = new (DateTime From, DateTime To)[queries];
        for (var i = 0; i < queries; i++)
        {
            var first = rnd.Next(0, size - windowPoints + 1);
            windows[i] = (data[first].Timestamp, data[first + windowPoints - 1].Timestamp);
        }

        // Correctness first: both methods must return exactly the same readings.
        var match = true;
        foreach (var (from, to) in windows.Take(20))
        {
            var linear = LinearRange(data, from, to);
            var viaTree = tree.Range(from, to).Select(e => e.Value).ToList();
            if (!linear.SequenceEqual(viaTree)) { match = false; break; }
        }

        var linearMicros = TimeAverageMicros(windows, w => LinearRange(data, w.From, w.To).Count);
        var avlMicros = TimeAverageMicros(windows, w => tree.Range(w.From, w.To).Count());

        int? naiveHeight = size <= UnbalancedDemoLimit ? UnbalancedHeight(data) : null;

        return new BenchmarkRow(size, queries, windowPoints,
            Math.Round(buildMs, 2), Math.Round(linearMicros, 2), Math.Round(avlMicros, 2),
            avlMicros > 0 ? Math.Round(linearMicros / avlMicros, 1) : 0,
            tree.Height, tree.MinimumPossibleHeight, naiveHeight, match);
    }

    /// <summary>The "unorganised list" approach: look at every reading and keep the ones in range.</summary>
    private static List<TelemetryPoint> LinearRange(List<TelemetryPoint> data, DateTime from, DateTime to)
    {
        var result = new List<TelemetryPoint>();
        foreach (var p in data)
            if (p.Timestamp >= from && p.Timestamp <= to) result.Add(p);
        return result;
    }

    private static double TimeAverageMicros((DateTime From, DateTime To)[] windows, Func<(DateTime From, DateTime To), int> run)
    {
        var sink = 0;
        var started = Stopwatch.GetTimestamp();
        foreach (var w in windows) sink += run(w);
        var micros = Stopwatch.GetElapsedTime(started).TotalMilliseconds * 1000.0 / windows.Length;
        GC.KeepAlive(sink);
        return micros;
    }

    /// <summary>Height of a plain (non-balancing) binary search tree built from the same sorted data.</summary>
    private static int UnbalancedHeight(List<TelemetryPoint> data)
    {
        var keys = data.Select(p => p.Timestamp).ToArray();
        var left = new int[keys.Length];
        var right = new int[keys.Length];
        Array.Fill(left, -1);
        Array.Fill(right, -1);
        var depthOf = new int[keys.Length];
        depthOf[0] = 1;
        var height = 1;

        for (var i = 1; i < keys.Length; i++)
        {
            var cur = 0;
            var depth = 1;
            while (true)
            {
                depth++;
                if (keys[i] < keys[cur])
                {
                    if (left[cur] == -1) { left[cur] = i; break; }
                    cur = left[cur];
                }
                else
                {
                    if (right[cur] == -1) { right[cur] = i; break; }
                    cur = right[cur];
                }
            }
            depthOf[i] = depth;
            if (depth > height) height = depth;
        }
        return height;
    }
}