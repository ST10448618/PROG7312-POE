using System.Text.RegularExpressions;

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

    private static readonly TimeSpan AssociationWindow = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(90);
    private const int MinSupport = 2;
    private const double ExpectedMin = 0;
    private const double ExpectedMax = 100;
    private const int PriorityBacklogThreshold = 8;

    // antecedent (e.g. "Critical:AA:BB:..") -> response key -> times observed
    private readonly Dictionary<string, Dictionary<string, int>> _associations = new();
    private readonly Queue<AlertTrigger> _recentAlerts = new();

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

    public void RecordAlert(string mac, string severity)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            PruneExpired(now);
            _recentAlerts.Enqueue(new AlertTrigger($"{severity}:{mac}", mac, severity, now));
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
        if (!_associations.TryGetValue(antecedent, out var row))
        {
            row = new Dictionary<string, int>();
            _associations[antecedent] = row;
        }
        row.TryGetValue(response, out var existing);
        row[response] = existing + by;
    }

    private void PruneExpired(DateTime now)
    {
        while (_recentAlerts.Count > 0 && now - _recentAlerts.Peek().At > AssociationWindow)
            _recentAlerts.Dequeue();
    }

    public List<Suggestion> GetSuggestions(int take = 5)
    {
        lock (_lock)
        {
            PruneExpired(DateTime.UtcNow);

            // PriorityQueue is a min-heap, so negate confidence to pop the strongest first.
            var ranked = new PriorityQueue<Suggestion, double>();
            var activeTriggers = _recentAlerts.GroupBy(a => a.Key).Select(g => g.Last());

            foreach (var trigger in activeTriggers)
            {
                if (!_associations.TryGetValue(trigger.Key, out var responses)) continue;
                var total = responses.Values.Sum();

                foreach (var (actionKey, count) in responses)
                {
                    if (count < MinSupport) continue;
                    var suggestion = BuildSuggestion(trigger, actionKey, count, (double)count / total);
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
                      $"{count} time(s) - {confidence * 100:0}% of your responses.";

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

        return insights
            .OrderBy(i => i.Severity == "Critical" ? 0 : 1)
            .ThenBy(i => i.Mac)
            .ToList();
    }

    public InsightsSnapshot Snapshot() => new(GetSuggestions(), GetInsights());

    private static string Humanize(string value) => Regex.Replace(value, "(?<!^)([A-Z])", " $1");
}