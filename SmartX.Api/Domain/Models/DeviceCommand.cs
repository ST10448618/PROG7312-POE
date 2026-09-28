namespace SmartX.Api.Domain.Models;

public enum CommandType { TriggerPump, ToggleRelay, ShutdownValve }

public class DeviceCommand
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string TargetMac { get; init; } = "";
    public CommandType Type { get; init; }
    public DateTime IssuedAt { get; init; } = DateTime.UtcNow;
    public bool IsPriority { get; init; }

    public override string ToString() => $"{Type} → {TargetMac} ({IssuedAt:HH:mm:ss})";
}