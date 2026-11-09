using System.Diagnostics.CodeAnalysis;
using SmartX.Api.Domain.Collections;
using SmartX.Api.Domain.Models;

namespace SmartX.Api.Services;

public class CommandStreamService
{
    private const int ProcessedLogCapacity = 50;

    private readonly Queue<QueuedReading> _standardQueue = new();

    private readonly PriorityQueue<QueuedReading, (int Rank, long Sequence)> _priorityQueue = new();

    private readonly Stack<DeviceCommand> _commandHistory = new();
    private readonly RingBuffer<ProcessedReading> _processedLog = new(ProcessedLogCapacity);

    // Capacity 1: a pending wake-up is either there or it isn't. The processor drains everything.
    private readonly SemaphoreSlim _workSignal = new(0, 1);
    private readonly object _lock = new();

    private long _sequence;
    private long _processedStandard;
    private long _processedPriority;

    // --- Queue<T>: standard FIFO lane ---
    public void EnqueueStandard(QueuedReading item)
    {
        lock (_lock) _standardQueue.Enqueue(item);
        SignalWork();
    }

    public int StandardQueueDepth { get { lock (_lock) return _standardQueue.Count; } }

    // --- PriorityQueue<TElement,TPriority>: critical alerts jump the line ---
    public void EnqueuePriority(QueuedReading item)
    {
        lock (_lock) _priorityQueue.Enqueue(item, (item.PriorityRank, _sequence++));
        SignalWork();
    }

    public int PriorityQueueDepth { get { lock (_lock) return _priorityQueue.Count; } }

    public int TotalDepth { get { lock (_lock) return _standardQueue.Count + _priorityQueue.Count; } }

    // --- Processing: the priority lane is always drained before the standard lane ---

    public bool TryProcessNext([NotNullWhen(true)] out ProcessedReading? processed)
    {
        lock (_lock)
        {
            QueuedReading item;
            QueueLane lane;
            var bypassed = 0;

            if (_priorityQueue.TryDequeue(out item, out _))
            {
                lane = QueueLane.Priority;
                bypassed = _standardQueue.Count; // how many standard items this alert jumped over
                _processedPriority++;
            }
            else if (_standardQueue.TryDequeue(out item))
            {
                lane = QueueLane.Standard;
                _processedStandard++;
            }
            else
            {
                processed = null;
                return false;
            }

            var now = DateTime.UtcNow;
            processed = new ProcessedReading(
                item.SensorId, item.Value, item.Severity, lane,
                item.EnqueuedAt, now, Math.Round((now - item.EnqueuedAt).TotalMilliseconds, 1), bypassed);

            _processedLog.Add(processed);
            return true;
        }
    }

    /// <summary>Completes as soon as there is something to process, so no polling timer is needed.</summary>
    public async Task WaitForWorkAsync(CancellationToken cancellationToken)
    {
        if (TotalDepth > 0) return; // work arrived while the processor was busy
        await _workSignal.WaitAsync(cancellationToken);
    }

    private void SignalWork()
    {
        if (_workSignal.CurrentCount != 0) return; // a wake-up is already pending
        try { _workSignal.Release(); }
        catch (SemaphoreFullException) { /* lost a race with another enqueue; the wake-up is pending anyway */ }
    }

    public long ProcessedStandardTotal { get { lock (_lock) return _processedStandard; } }
    public long ProcessedPriorityTotal { get { lock (_lock) return _processedPriority; } }

    /// <summary>Most recent first. Enumerable.Reverse is explicit on purpose: in C# 14, array.Reverse() binds to the in-place span method.</summary>
    public List<ProcessedReading> GetProcessedLog(int take = 20)
    {
        lock (_lock) return Enumerable.Reverse(_processedLog.ToArray()).Take(take).ToList();
    }

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