namespace SmartX.Api.Domain.Models;

public enum QueueLane { Standard, Priority }

public readonly record struct QueuedReading(string SensorId, double Value, string Severity, DateTime EnqueuedAt)
{
    /// <summary>Lower rank is processed first. Disconnects and Critical share rank 0.</summary>
    public int PriorityRank => Severity switch
    {
        "Critical" or "Disconnected" => 0,
        "Warning" => 1,
        _ => 2
    };
}

/// <summary>One entry in the processing log: what was handled, from which lane, and how long it waited.</summary>
public record ProcessedReading(
    string SensorId,
    double Value,
    string Severity,
    QueueLane Lane,
    DateTime EnqueuedAt,
    DateTime ProcessedAt,
    double WaitMs,
    int StandardItemsBypassed);