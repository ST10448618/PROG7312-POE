namespace SmartX.Api.Domain.Models;

public class LiveDeviceState
{
    public string MacAddress { get; init; } = "";
    public string Location { get; set; } = "";
    public string Category { get; set; } = "";
    public string Status { get; set; } = "Online";
    public double? LastValue { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}