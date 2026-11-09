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