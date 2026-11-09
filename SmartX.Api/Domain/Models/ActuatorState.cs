namespace SmartX.Api.Domain.Models;

public readonly record struct ActuatorState(bool PumpRunning, bool RelayOn, bool ValveOpen)
{
    /// <summary>Resting state: pump idle, relay off, valve open.</summary>
    public static ActuatorState Default => new(false, false, true);

    public ActuatorState Apply(CommandType type) => type switch
    {
        CommandType.TriggerPump => this with { PumpRunning = true },
        CommandType.ToggleRelay => this with { RelayOn = !RelayOn },
        CommandType.ShutdownValve => this with { ValveOpen = false },
        _ => this
    };
}