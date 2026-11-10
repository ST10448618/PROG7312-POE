using System.Text.RegularExpressions;
using SmartX.Api.Domain.Collections;

namespace SmartX.Api.Services;

public record Suggestion(
    string Kind, string Title, string Detail,
    string? TargetMac, string? CommandType,
    string? Category, string? State, string? Text,
    double Confidence, int Support, string ActionKey);

public record Insight(string Severity, string Title, string Detail, string? Mac);

public record InsightsSnapshot(List<Suggestion> Suggestions, List<Insight> Insights);

public class PredictiveActionEngine
{
    private sealed record AlertTrigger(string Key, string Mac, string Severity, DateTime At);

    /// <summary>How often a response followed an alert. Weight fades with time; Count is the raw tally.</summary>
    private sealed class Association
    {
        public int Count;
        public double Weight;
        public DateTime UpdatedAt;
    }

    private readonly record struct ReadingPoint(DateTime At, double Value);

    private readonly record struct Forecast(double SlopePerMinute, double RSquared, double MinutesToBreach, bool UpperBound, int Points);

    // --- suggestion tuning ---
    private static readonly TimeSpan AssociationWindow = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan PatternHalfLife = TimeSpan.FromHours(4); // old habits fade unless they are repeated
    private const int MinSupport = 2;        // raw observations needed before a pattern is trusted
    private const double MinWeight = 1.0;    // ...and it must still carry this much decayed weight

    // --- insight tuning ---
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(90);
    private const double ExpectedMin = 0;
    private const double ExpectedMax = 100;
    private const int PriorityBacklogThreshold = 8;

    private static readonly TimeSpan OffenderWindow = TimeSpan.FromMinutes(60);
    private const int OffenderThreshold = 3;

    private const int TrendWindow = 20;      // readings kept per device
    private const int MinTrendPoints = 6;
    private const double MinRSquared = 0.6;  // ignore noisy fits so random jitter never raises a forecast
    private static readonly TimeSpan TrendMaxAge = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ForecastHorizon = TimeSpan.FromMinutes(30);

    // antecedent (e.g. "Critical:AA:BB:..") -> response key -> how often it followed
    private readonly Dictionary<string, Dictionary<string, Association>> _associations = new();
    private readonly Queue<AlertTrigger> _recentAlerts = new();

    // mac -> timestamps of recent Critical/Disconnected alerts (sliding window)
    private readonly Dictionary<string, Queue<DateTime>> _offences = new();

    // mac -> recent readings for the trend forecast
    private readonly Dictionary<string, RingBuffer<ReadingPoint>> _readings = new();

    private readonly LiveDeviceRegistry _registry;
    private readonly ActiveFaultTracker _faults;
    private readonly CommandStreamService _commands;
    private readonly object _lock = new();

    public PredictiveActionEngine(LiveDeviceRegistry registry, ActiveFaultTracker faults, CommandStreamService commands)
    {
        _registry = registry;
        _faults = faults;
        _commands = commands;
    }

    // ---------------------------------------------------------------- learning

    public void RecordAlert(string mac, string severity)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            PruneExpired(now);
            _recentAlerts.Enqueue(new AlertTrigger($"{severity}:{mac}", mac, severity, now));

            if (severity is "Critical" or "Disconnected")
            {
                if (!_offences.TryGetValue(mac, out var times))
                {
                    times = new Queue<DateTime>();
                    _offences[mac] = times;
                }
                times.Enqueue(now);
                PruneOffences(times, now);
            }
        }
    }

    public void RecordReading(string mac, double value)
    {
        lock (_lock)
        {
            if (!_readings.TryGetValue(mac, out var buffer))
            {
                buffer = new RingBuffer<ReadingPoint>(TrendWindow);
                _readings[mac] = buffer;
            }
            buffer.Add(new ReadingPoint(DateTime.UtcNow, value));
        }
    }

    public void RecordUserAction(string actionKey)
    {
        lock (_lock)
        {
            PruneExpired(DateTime.UtcNow);
            var distinctTriggers = new HashSet<string>(_recentAlerts.Select(a => a.Key));
            foreach (var trigger in distinctTriggers)
                Increment(trigger, actionKey, 1);
        }
    }

    public void RecordSearch(string? text, string? category, string? state)
    {
        var t = (text ?? "").Trim().ToLowerInvariant();
        var c = (category ?? "").Trim();
        var s = (state ?? "").Trim();
        if (t.Length == 0 && c.Length == 0 && s.Length == 0) return;
        RecordUserAction($"Search:{c}|{s}|{t}");
    }

    public void SeedAssociation(string antecedentKey, string actionKey, int count)
    {
        lock (_lock) Increment(antecedentKey, actionKey, count);
    }

    private void Increment(string antecedent, string response, int by)
    {
        var now = DateTime.UtcNow;
        if (!_associations.TryGetValue(antecedent, out var row))
        {
            row = new Dictionary<string, Association>();
            _associations[antecedent] = row;
        }

        if (!row.TryGetValue(response, out var assoc))
        {
            assoc = new Association { UpdatedAt = now };
            row[response] = assoc;
        }

        // Decay what was already there, then add the new observation at full strength.
        assoc.Weight = DecayedWeight(assoc, now) + by;
        assoc.Count += by;
        assoc.UpdatedAt = now;
    }

    private static double DecayedWeight(Association a, DateTime now) =>
        a.Weight * Math.Pow(0.5, Math.Max(0, (now - a.UpdatedAt).TotalSeconds) / PatternHalfLife.TotalSeconds);

    private void PruneExpired(DateTime now)
    {
        while (_recentAlerts.Count > 0 && now - _recentAlerts.Peek().At > AssociationWindow)
            _recentAlerts.Dequeue();
    }

    private static void PruneOffences(Queue<DateTime> times, DateTime now)
    {
        while (times.Count > 0 && now - times.Peek() > OffenderWindow)
            times.Dequeue();
    }

    // ------------------------------------------------------------- suggestions

    public List<Suggestion> GetSuggestions(int take = 5)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            PruneExpired(now);

            // PriorityQueue is a min-heap, so negate confidence to pop the strongest first.
            var ranked = new PriorityQueue<Suggestion, double>();
            var activeTriggers = _recentAlerts.GroupBy(a => a.Key).Select(g => g.Last());

            foreach (var trigger in activeTriggers)
            {
                if (!_associations.TryGetValue(trigger.Key, out var responses)) continue;

                // Confidence is each response's share of the *decayed* total, so recent behaviour dominates.
                var weights = responses.ToDictionary(r => r.Key, r => DecayedWeight(r.Value, now));
                var total = weights.Values.Sum();
                if (total <= 0) continue;

                foreach (var (actionKey, assoc) in responses)
                {
                    var weight = weights[actionKey];
                    if (assoc.Count < MinSupport || weight < MinWeight) continue;

                    var suggestion = BuildSuggestion(trigger, actionKey, assoc.Count, weight / total);
                    if (suggestion is not null)
                        ranked.Enqueue(suggestion, -suggestion.Confidence);
                }
            }

            var result = new List<Suggestion>();
            var emitted = new HashSet<string>();
            while (ranked.Count > 0 && result.Count < take)
            {
                var next = ranked.Dequeue();
                if (emitted.Add(next.ActionKey)) result.Add(next);
            }
            return result;
        }
    }

    private Suggestion? BuildSuggestion(AlertTrigger trigger, string actionKey, int count, double confidence)
    {
        var parts = actionKey.Split(':', 2);
        var because = $"After {trigger.Severity.ToLowerInvariant()} alerts on {trigger.Mac}, you did this " +
                      $"{count} time(s) - {confidence * 100:0}% of your recent responses.";

        if (parts[0] == "Command" && parts.Length == 2)
        {
            var cmd = parts[1].Split(':', 2); // type : mac
            if (cmd.Length != 2 || _registry.Get(cmd[1]) is null) return null;
            return new Suggestion("Command", $"{Humanize(cmd[0])} on {cmd[1]}", because,
                cmd[1], cmd[0], null, null, null, confidence, count, actionKey);
        }

        if (parts[0] == "Search" && parts.Length == 2)
        {
            var f = parts[1].Split('|'); // category | state | text
            if (f.Length != 3) return null;
            var label = string.Join(" · ", f.Where(x => !string.IsNullOrWhiteSpace(x)));
            return new Suggestion("Search", $"Filter devices: {label}", because,
                null, null, f[0], f[1], f[2], confidence, count, actionKey);
        }

        return null;
    }

    // ---------------------------------------------------------------- insights

    public List<Insight> GetInsights()
    {
        var now = DateTime.UtcNow;
        var insights = new List<Insight>();
        var faulted = new HashSet<string>(_faults.GetAllFaulted()); // O(1) membership

        foreach (var mac in faulted)
            insights.Add(new Insight("Critical", $"{mac} is disconnected",
                "No telemetry until it reconnects. Check power and the mesh link before sending commands.", mac));

        foreach (var d in _registry.GetAll())
        {
            if (faulted.Contains(d.MacAddress)) continue;

            if (d.LastValue is double v && (v < ExpectedMin || v > ExpectedMax))
                insights.Add(new Insight("Warning", $"{d.MacAddress} reading out of range",
                    $"Last value {v:0.0} is outside the expected {ExpectedMin}-{ExpectedMax} range.", d.MacAddress));
            else if (now - d.LastSeen > StaleAfter)
                insights.Add(new Insight("Warning", $"{d.MacAddress} has gone quiet",
                    $"No telemetry for {(now - d.LastSeen).TotalSeconds:0}s.", d.MacAddress));
        }

        if (_commands.PriorityQueueDepth >= PriorityBacklogThreshold)
            insights.Add(new Insight("Warning", "Priority queue backlog",
                $"{_commands.PriorityQueueDepth} critical/warning readings are waiting to be processed.", null));

        lock (_lock)
        {
            AddRepeatOffenderInsights(insights, now);
            AddTrendInsights(insights, faulted, now);
        }

        return insights
            .OrderBy(i => i.Severity == "Critical" ? 0 : 1)
            .ThenBy(i => i.Mac)
            .ToList();
    }

    /// <summary>History-based: a device that keeps raising Critical or Disconnected alerts has a recurring fault.</summary>
    private void AddRepeatOffenderInsights(List<Insight> insights, DateTime now)
    {
        foreach (var (mac, times) in _offences)
        {
            PruneOffences(times, now);
            if (times.Count < OffenderThreshold) continue;

            var severity = times.Count >= OffenderThreshold * 2 ? "Critical" : "Warning";
            insights.Add(new Insight(severity, $"{mac} is a repeat offender",
                $"{times.Count} critical or disconnect alerts in the last {OffenderWindow.TotalMinutes:0} minutes. " +
                "This looks like a recurring fault rather than a one-off, so inspect the device or its mesh link.", mac));
        }
    }

    /// <summary>Trend-based: fit a line through recent in-range readings and warn before the range is breached.</summary>
    private void AddTrendInsights(List<Insight> insights, HashSet<string> faulted, DateTime now)
    {
        foreach (var (mac, buffer) in _readings)
        {
            if (faulted.Contains(mac)) continue;

            var points = buffer.ToArray()
                .Where(p => p.Value >= ExpectedMin && p.Value <= ExpectedMax && now - p.At <= TrendMaxAge)
                .ToArray();

            if (!TryForecast(points, out var f)) continue;

            var when = f.MinutesToBreach < 1 ? "in under a minute" : $"in about {f.MinutesToBreach:0} min";
            var direction = f.UpperBound ? "upwards" : "downwards";
            insights.Add(new Insight("Warning", $"{mac} is trending {direction}",
                $"Linear trend of {f.SlopePerMinute:+0.0;-0.0} units/min over the last {f.Points} readings " +
                $"(R² {f.RSquared:0.00}). Expected to leave the {ExpectedMin}-{ExpectedMax} range {when}.", mac));
        }
    }

    /// <summary>Ordinary least squares over (minutes, value). Returns false for noisy, flat or distant trends.</summary>
    private static bool TryForecast(ReadingPoint[] points, out Forecast forecast)
    {
        forecast = default;
        if (points.Length < MinTrendPoints) return false;

        var t0 = points[0].At;
        var xs = points.Select(p => (p.At - t0).TotalMinutes).ToArray();
        var ys = points.Select(p => p.Value).ToArray();
        var meanX = xs.Average();
        var meanY = ys.Average();

        double sxx = 0, sxy = 0, syy = 0;
        for (var i = 0; i < xs.Length; i++)
        {
            var dx = xs[i] - meanX;
            var dy = ys[i] - meanY;
            sxx += dx * dx;
            sxy += dx * dy;
            syy += dy * dy;
        }

        if (sxx < 1e-9 || syy < 1e-9) return false; // no time spread, or a perfectly flat series

        var slope = sxy / sxx;
        var rSquared = sxy * sxy / (sxx * syy);
        if (rSquared < MinRSquared || Math.Abs(slope) < 1e-6) return false;

        var fittedNow = meanY + slope * (xs[^1] - meanX);
        var target = slope > 0 ? ExpectedMax : ExpectedMin;
        var minutes = Math.Max(0, (target - fittedNow) / slope);
        if (minutes > ForecastHorizon.TotalMinutes) return false;

        forecast = new Forecast(slope, rSquared, minutes, slope > 0, points.Length);
        return true;
    }

    public InsightsSnapshot Snapshot() => new(GetSuggestions(), GetInsights());

    private static string Humanize(string value) => Regex.Replace(value, "(?<!^)([A-Z])", " $1");
}