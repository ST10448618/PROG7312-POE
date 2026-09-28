using SmartX.Api.Domain.Models;

namespace SmartX.Api.Services;

public class CommandStreamService
{
    private readonly Queue<string> _standardQueue = new();
    private readonly PriorityQueue<string, int> _priorityQueue = new();
    private readonly Stack<DeviceCommand> _commandHistory = new();
    private readonly object _lock = new();

    // --- Queue<T>: standard FIFO processing ---
    public void EnqueueStandard(string sensorId)
    {
        lock (_lock) _standardQueue.Enqueue(sensorId);
    }

    public string? DequeueStandard()
    {
        lock (_lock) return _standardQueue.TryDequeue(out var item) ? item : null;
    }

    public int StandardQueueDepth { get { lock (_lock) return _standardQueue.Count; } }

    // --- PriorityQueue<TElement,TPriority>: critical alerts jump the line ---
    // Lower priority value = processed first. Critical = 0, Warning = 1.
    public void EnqueuePriority(string sensorId, int priority)
    {
        lock (_lock) _priorityQueue.Enqueue(sensorId, priority);
    }

    public string? DequeuePriority()
    {
        lock (_lock) return _priorityQueue.TryDequeue(out var item, out _) ? item : null;
    }

    public int PriorityQueueDepth { get { lock (_lock) return _priorityQueue.Count; } }

    // --- Stack<T>: manual override command history, undoable ---
    public void PushCommand(DeviceCommand command)
    {
        lock (_lock) _commandHistory.Push(command);
    }

    public DeviceCommand? PeekLastCommand()
    {
        lock (_lock) return _commandHistory.TryPeek(out var cmd) ? cmd : null;
    }

    public DeviceCommand? PopLastCommand()
    {
        lock (_lock) return _commandHistory.TryPop(out var cmd) ? cmd : null;
    }

    public List<DeviceCommand> GetHistorySnapshot()
    {
        lock (_lock) return _commandHistory.ToList(); // most recent first, matches Stack enumeration order
    }
}