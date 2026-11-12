using SmartX.Api.Domain.Models;
using SmartX.Api.Services;

namespace SmartX.Tests.Services;

public class CommandStreamServiceTests
{
    private const string Pump = "AA:BB:CC:00:01:03";
    private const string Other = "AA:BB:CC:00:01:07";

    private static QueuedReading Reading(string sensor, string severity) =>
        new(sensor, 1.0, severity, DateTime.UtcNow);

    private static List<string> Drain(CommandStreamService svc)
    {
        var order = new List<string>();
        while (svc.TryProcessNext(out var item)) order.Add(item!.SensorId);
        return order;
    }

    // ---------------------------------------------------------- queue lanes

    [Fact]
    public void CriticalAlert_JumpsAheadOfEveryQueuedStandardReading()
    {
        var svc = new CommandStreamService();
        svc.EnqueueStandard(Reading("S1", "Normal"));
        svc.EnqueueStandard(Reading("S2", "Normal"));
        svc.EnqueueStandard(Reading("S3", "Normal"));
        svc.EnqueuePriority(Reading("C1", "Critical"));

        Assert.True(svc.TryProcessNext(out var first));

        Assert.Equal("C1", first!.SensorId);
        Assert.Equal(QueueLane.Priority, first.Lane);
        Assert.Equal(3, first.StandardItemsBypassed);   // it skipped the three waiting readings
    }

    [Fact]
    public void StandardLane_IsFirstInFirstOut()
    {
        var svc = new CommandStreamService();
        foreach (var id in new[] { "S1", "S2", "S3" }) svc.EnqueueStandard(Reading(id, "Normal"));

        Assert.Equal(new[] { "S1", "S2", "S3" }, Drain(svc));
    }

    [Fact]
    public void PriorityLane_ServesCriticalBeforeWarning_AndKeepsArrivalOrderWithinARank()
    {
        var svc = new CommandStreamService();
        svc.EnqueuePriority(Reading("W1", "Warning"));
        svc.EnqueuePriority(Reading("C1", "Critical"));
        svc.EnqueuePriority(Reading("W2", "Warning"));
        svc.EnqueuePriority(Reading("D1", "Disconnected"));   // shares rank 0 with Critical

        Assert.Equal(new[] { "C1", "D1", "W1", "W2" }, Drain(svc));
    }

    [Fact]
    public void PriorityLane_IsFullyDrainedBeforeAnyStandardItem()
    {
        var svc = new CommandStreamService();
        svc.EnqueueStandard(Reading("S1", "Normal"));
        svc.EnqueuePriority(Reading("C1", "Critical"));
        svc.EnqueueStandard(Reading("S2", "Normal"));
        svc.EnqueuePriority(Reading("C2", "Critical"));

        Assert.Equal(new[] { "C1", "C2", "S1", "S2" }, Drain(svc));
    }

    [Fact]
    public void TryProcessNext_OnAnEmptyService_ReturnsFalse()
    {
        var svc = new CommandStreamService();

        Assert.False(svc.TryProcessNext(out var item));
        Assert.Null(item);
    }

    [Fact]
    public void DepthsAndTotals_TrackWhatIsWaitingAndWhatWasProcessed()
    {
        var svc = new CommandStreamService();
        svc.EnqueueStandard(Reading("S1", "Normal"));
        svc.EnqueueStandard(Reading("S2", "Normal"));
        svc.EnqueuePriority(Reading("C1", "Critical"));

        Assert.Equal(2, svc.StandardQueueDepth);
        Assert.Equal(1, svc.PriorityQueueDepth);
        Assert.Equal(3, svc.TotalDepth);

        Drain(svc);

        Assert.Equal(0, svc.TotalDepth);
        Assert.Equal(2, svc.ProcessedStandardTotal);
        Assert.Equal(1, svc.ProcessedPriorityTotal);
    }

    [Fact]
    public void ProcessedLog_ListsTheMostRecentFirst_AndRespectsTake()
    {
        var svc = new CommandStreamService();
        foreach (var id in new[] { "S1", "S2", "S3", "S4" }) svc.EnqueueStandard(Reading(id, "Normal"));
        Drain(svc);

        var log = svc.GetProcessedLog(take: 2);

        Assert.Equal(new[] { "S4", "S3" }, log.Select(p => p.SensorId));
        Assert.All(log, p => Assert.True(p.WaitMs >= 0));
    }

    // ----------------------------------------------------------- wake-ups

    [Fact]
    public async Task WaitForWork_CompletesAtOnce_WhenWorkIsAlreadyQueued()
    {
        var svc = new CommandStreamService();
        svc.EnqueueStandard(Reading("S1", "Normal"));

        await svc.WaitForWorkAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task WaitForWork_WakesAsSoonAsSomethingIsEnqueued()
    {
        var svc = new CommandStreamService();
        var waiting = svc.WaitForWorkAsync(CancellationToken.None);
        Assert.False(waiting.IsCompleted);

        svc.EnqueuePriority(Reading("C1", "Critical"));

        await waiting.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task WaitForWork_ObservesCancellation()
    {
        var svc = new CommandStreamService();
        using var cts = new CancellationTokenSource();
        var waiting = svc.WaitForWorkAsync(cts.Token);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }

    // -------------------------------------------------- command stack: undo

    private static LiveDeviceRegistry RegistryWithTwoDevices()
    {
        var registry = new LiveDeviceRegistry();
        registry.Register(Pump, "Facility A/Zone 1", "Actuator");
        registry.Register(Other, "Facility A/Zone 2", "Actuator");
        return registry;
    }

    [Fact]
    public void IssueCommand_AppliesTheEffect_AndUndoRestoresIt()
    {
        var svc = new CommandStreamService();
        var registry = RegistryWithTwoDevices();

        var command = svc.IssueCommand(registry, Pump, CommandType.TriggerPump);

        Assert.False(command.Before.PumpRunning);
        Assert.True(command.After.PumpRunning);
        Assert.True(registry.Get(Pump)!.Actuators.PumpRunning);

        var undone = svc.UndoLastCommand(registry);

        Assert.Equal(command.Id, undone!.Id);
        Assert.False(registry.Get(Pump)!.Actuators.PumpRunning);
    }

    [Fact]
    public void Undo_IsLastInFirstOut_AcrossDevices()
    {
        var svc = new CommandStreamService();
        var registry = RegistryWithTwoDevices();

        svc.IssueCommand(registry, Pump, CommandType.TriggerPump);
        svc.IssueCommand(registry, Other, CommandType.ShutdownValve);

        Assert.False(registry.Get(Other)!.Actuators.ValveOpen);

        Assert.Equal(Other, svc.UndoLastCommand(registry)!.TargetMac);
        Assert.True(registry.Get(Other)!.Actuators.ValveOpen);
        Assert.True(registry.Get(Pump)!.Actuators.PumpRunning);   // the earlier command is untouched

        Assert.Equal(Pump, svc.UndoLastCommand(registry)!.TargetMac);
        Assert.False(registry.Get(Pump)!.Actuators.PumpRunning);
    }

    [Fact]
    public void Undo_OfAToggle_RestoresThePreviousStateEachTime()
    {
        var svc = new CommandStreamService();
        var registry = RegistryWithTwoDevices();

        svc.IssueCommand(registry, Pump, CommandType.ToggleRelay);   // off -> on
        svc.IssueCommand(registry, Pump, CommandType.ToggleRelay);   // on -> off
        Assert.False(registry.Get(Pump)!.Actuators.RelayOn);

        svc.UndoLastCommand(registry);
        Assert.True(registry.Get(Pump)!.Actuators.RelayOn);

        svc.UndoLastCommand(registry);
        Assert.False(registry.Get(Pump)!.Actuators.RelayOn);
    }

    [Fact]
    public void Undo_WithNothingToUndo_ReturnsNull()
    {
        var svc = new CommandStreamService();

        Assert.Null(svc.UndoLastCommand(RegistryWithTwoDevices()));
    }

    [Fact]
    public void IssueCommand_ForAnUnknownDevice_ThrowsAndRecordsNothing()
    {
        var svc = new CommandStreamService();

        Assert.Throws<KeyNotFoundException>(() =>
            svc.IssueCommand(RegistryWithTwoDevices(), "AA:BB:CC:FF:FF:FF", CommandType.TriggerPump));

        Assert.Empty(svc.GetHistorySnapshot());
    }

    [Fact]
    public void HistorySnapshot_ListsTheMostRecentCommandFirst()
    {
        var svc = new CommandStreamService();
        var registry = RegistryWithTwoDevices();

        svc.IssueCommand(registry, Pump, CommandType.TriggerPump);
        svc.IssueCommand(registry, Other, CommandType.ToggleRelay);

        var history = svc.GetHistorySnapshot();

        Assert.Equal(new[] { CommandType.ToggleRelay, CommandType.TriggerPump }, history.Select(c => c.Type));
        Assert.Equal(CommandType.ToggleRelay, svc.PeekLastCommand()!.Type);
    }

    [Fact]
    public void ActuatorState_StartsAtRest_AndAppliesEachCommand()
    {
        var rest = ActuatorState.Default;

        Assert.False(rest.PumpRunning);
        Assert.False(rest.RelayOn);
        Assert.True(rest.ValveOpen);

        Assert.True(rest.Apply(CommandType.TriggerPump).PumpRunning);
        Assert.True(rest.Apply(CommandType.ToggleRelay).RelayOn);
        Assert.False(rest.Apply(CommandType.ShutdownValve).ValveOpen);
        Assert.False(rest.Apply(CommandType.ToggleRelay).Apply(CommandType.ToggleRelay).RelayOn);
    }
}
