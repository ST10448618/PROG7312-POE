namespace SmartX.Client.Models;

public class DeviceCommandDto
{
    public string Id { get; set; } = "";
    public string TargetMac { get; set; } = "";
    public string Type { get; set; } = "";
    public DateTime IssuedAt { get; set; }
    public bool IsPriority { get; set; }
}

public class QueueDepthDto
{
    public int StandardDepth { get; set; }
    public int PriorityDepth { get; set; }
}