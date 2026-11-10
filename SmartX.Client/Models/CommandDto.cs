namespace SmartX.Client.Models;

public class DeviceCommandDto
{
    public string Id { get; set; } = "";
    public string TargetMac { get; set; } = "";
    public string Type { get; set; } = "";
    public DateTime IssuedAt { get; set; }
    public bool IsPriority { get; set; }
    public ActuatorStateDto Before { get; set; } = new();
    public ActuatorStateDto After { get; set; } = new();
}

public class ActuatorStateDto
{
    public bool PumpRunning { get; set; }
    public bool RelayOn { get; set; }
    public bool ValveOpen { get; set; } = true;
}

public class ProcessedReadingDto
{
    public string SensorId { get; set; } = "";
    public double Value { get; set; }
    public string Severity { get; set; } = "";
    public string Lane { get; set; } = "";
    public DateTime EnqueuedAt { get; set; }
    public DateTime ProcessedAt { get; set; }
    public double WaitMs { get; set; }
    public int StandardItemsBypassed { get; set; }
}

public class QueueDepthDto
{
    public int StandardDepth { get; set; }
    public int PriorityDepth { get; set; }
}

public class TimelineBucketDto
{
    public DateTime Bucket { get; set; }
    public int Count { get; set; }
    public List<TimelineReadingDto> Sensors { get; set; } = new();
}

public class TimelineReadingDto
{
    public string SensorId { get; set; } = "";
    public string RawValue { get; set; } = "";
    public string Unit { get; set; } = "";
}

public class SuggestionDto
{
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string? TargetMac { get; set; }
    public string? CommandType { get; set; }
    public string? Category { get; set; }
    public string? State { get; set; }
    public string? Text { get; set; }
    public double Confidence { get; set; }
    public int Support { get; set; }
    public string ActionKey { get; set; } = "";
}

public class InsightDto
{
    public string Severity { get; set; } = "";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string? Mac { get; set; }
}

public class InsightsSnapshotDto
{
    public List<SuggestionDto> Suggestions { get; set; } = new();
    public List<InsightDto> Insights { get; set; } = new();
}