using Microsoft.AspNetCore.SignalR.Client;
using SmartX.Client.Models;

namespace SmartX.Client.Services;

public sealed class QueueDepthEventDto
{
    public int Standard { get; set; }
    public int Priority { get; set; }
}

public sealed class TelemetryEventDto
{
    public string SensorId { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
}

public class TelemetryStore : IAsyncDisposable
{
    private const int MaxHeatCells = 30;
    private const int MaxLiveEvents = 15;
    private const int SnapshotLogCount = 200;
    private static readonly TimeSpan ActiveCriticalWindow = TimeSpan.FromMinutes(2);

    private readonly SensorApiClient _api;
    private readonly ApiConfig _config;
    private readonly SemaphoreSlim _snapshotLock = new(1, 1);
    private readonly SemaphoreSlim _hubLock = new(1, 1);
    private readonly List<AnomalyCellDto> _activeCritical = new();
    private HubConnection? _hub;
    private bool _snapshotOk;
    private bool _disposed;

    public TelemetryStore(SensorApiClient api, ApiConfig config)
    {
        _api = api;
        _config = config;
    }

    public event Action? Changed;
    private void Notify() => Changed?.Invoke();

    // ---------- connection ----------
    public string ConnectionState { get; private set; } = "Connecting…";
    public bool IsLoading { get; private set; } = true;

    // ---------- shared live data ----------
    public List<LiveDeviceStateDto> Devices { get; private set; } = new();
    public QueueDepthDto? QueueDepths { get; private set; }
    public List<DeviceCommandDto> CommandHistory { get; private set; } = new();
    public List<string> ActiveFaults { get; private set; } = new();
    public InsightsSnapshotDto Insights { get; private set; } = new();
    public List<TimelineBucketDto> Timeline { get; private set; } = new();
    public List<string> LiveEvents { get; } = new();

    // Heat map + chart data (Overview)
    public List<string> HeatRows { get; } = new();
    public Dictionary<string, List<AnomalyCellDto>> HeatCells { get; } = new();
    public HashSet<string> ChartHidden { get; } = new();
    /// <summary>Critical readings from the last 2 minutes (expired entries drop off on read).</summary>
    public List<AnomalyCellDto> ActiveCritical
    {
        get
        {
            var cutoff = DateTime.UtcNow - ActiveCriticalWindow;
            _activeCritical.RemoveAll(a => a.Timestamp.ToUniversalTime() < cutoff);
            return _activeCritical;
        }
    }

    // ---------- UI state preserved across navigation ----------
    public bool ChartFullRange { get; set; }

    public string CmdSearchText { get; set; } = "";
    public string CmdFilterCategory { get; set; } = "";
    public string CmdFilterState { get; set; } = "";
    public string CmdSelectedTargetMac { get; set; } = "";
    public string? CmdMessage { get; set; }
    public bool CmdMessageOk { get; set; }

    public string LogSearch { get; set; } = "";
    public string LogSeverity { get; set; } = "All";

    public SensorDto IngestionDraft { get; set; } = new();
    public string IngestionPath { get; set; } = "";
    public double WattsA { get; set; } = 120;
    public double WattsB { get; set; } = 80;

    // ---------- startup ----------
    public async Task InitializeAsync()
    {
        await RefreshSnapshotAsync(onlyIfNeeded: true);
        _ = EnsureHubAsync(); // navigation never waits on the websocket handshake
    }

    private async Task RefreshSnapshotAsync(bool onlyIfNeeded = false)
    {
        await _snapshotLock.WaitAsync();
        try
        {
            if (onlyIfNeeded && _snapshotOk) return;
            await LoadSnapshotAsync();
        }
        finally { _snapshotLock.Release(); }
    }

    private async Task LoadSnapshotAsync()
    {
        var devices = await Try<List<LiveDeviceStateDto>>(() => _api.GetDeviceRegistryAsync());
        if (devices is not null) Devices = devices;
        _snapshotOk = devices is not null;

        var faults = await Try<List<string>>(() => _api.GetActiveFaultsAsync());
        if (faults is not null) ActiveFaults = faults;

        var insights = await Try<InsightsSnapshotDto>(() => _api.GetInsightsAsync());
        if (insights is not null) Insights = insights;

        var depths = await Try<QueueDepthDto>(() => _api.GetQueueDepthsAsync());
        if (depths is not null) QueueDepths = depths;

        var history = await Try<List<DeviceCommandDto>>(() => _api.GetCommandHistoryAsync());
        if (history is not null) CommandHistory = history;

        var timeline = await Try<List<TimelineBucketDto>>(() => _api.GetTimelineAsync());
        if (timeline is not null) Timeline = timeline;

        var logs = await Try<List<AnomalyLogDto>>(() => _api.GetAnomalyLogsAsync("All", "", SnapshotLogCount));
        if (logs is not null)
        {
            HeatRows.Clear();
            HeatCells.Clear();
            _activeCritical.Clear();
            foreach (var log in logs.OrderBy(l => l.Timestamp))
                AddHeatCell(new AnomalyCellDto
                {
                    SensorId = log.SensorId, Value = log.Value, Score = log.Score,
                    Colour = log.Colour, Timestamp = log.Timestamp
                });
        }

        IsLoading = false;
        Notify();
    }

    // ---------- explicit refreshes the pages can trigger ----------
    public async Task RefreshCommandDataAsync()
    {
        var depths = await Try<QueueDepthDto>(() => _api.GetQueueDepthsAsync());
        if (depths is not null) QueueDepths = depths;
        var history = await Try<List<DeviceCommandDto>>(() => _api.GetCommandHistoryAsync());
        if (history is not null) CommandHistory = history;
        Notify();
    }

    public async Task RefreshTimelineAsync()
    {
        var timeline = await Try<List<TimelineBucketDto>>(() => _api.GetTimelineAsync());
        if (timeline is not null) Timeline = timeline;
        Notify();
    }

    public async Task RefreshDevicesAsync()
    {
        var devices = await Try<List<LiveDeviceStateDto>>(() => _api.GetDeviceRegistryAsync());
        if (devices is not null) Devices = devices;
        Notify();
    }

    // ---------- the one shared hub connection ----------
    private async Task EnsureHubAsync()
    {
        if (!await _hubLock.WaitAsync(0)) return; // an attempt is already running
        try
        {
            _hub ??= BuildHub();
            while (_hub.State == HubConnectionState.Disconnected && !_disposed)
            {
                SetConnection("Connecting…");
                try
                {
                    await _hub.StartAsync();
                    SetConnection("Connected");
                    if (!_snapshotOk) await RefreshSnapshotAsync(onlyIfNeeded: true);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                    SetConnection("Disconnected");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
        }
        finally { _hubLock.Release(); }
    }

    private HubConnection BuildHub()
    {
        var hub = new HubConnectionBuilder()
            .WithUrl($"{_config.BaseUrl}/hubs/telemetry")
            .WithAutomaticReconnect()
            .Build();

        hub.Reconnecting += error =>
        {
            SetConnection("Reconnecting…");
            return Task.CompletedTask;
        };

        hub.Reconnected += async connectionId =>
        {
            SetConnection("Connected");
            await RefreshSnapshotAsync(); // catch up on anything missed while offline
        };

        // Parameter is named 'error' (not '_') so that '_ = ...' below is a real discard.
        hub.Closed += error =>
        {
            SetConnection("Disconnected");
            if (!_disposed) _ = EnsureHubAsync();
            return Task.CompletedTask;
        };

        hub.On<AnomalyCellDto>("AnomalyUpdate", cell => { AddHeatCell(cell); Notify(); });

        hub.On<TelemetryEventDto>("TelemetryIngested", e =>
        {
            LiveEvents.Insert(0, $"{e.Timestamp.ToLocalTime():HH:mm:ss}  {e.SensorId}  →  {e.Value:0.0}");
            if (LiveEvents.Count > MaxLiveEvents) LiveEvents.RemoveAt(LiveEvents.Count - 1);
            Notify();
        });

        hub.On<List<LiveDeviceStateDto>>("DeviceRegistryUpdated", devices => { Devices = devices; Notify(); });
        hub.On<List<string>>("FaultSetChanged", faults => { ActiveFaults = faults; Notify(); });
        hub.On<InsightsSnapshotDto>("InsightsUpdated", snapshot => { Insights = snapshot; Notify(); });

        hub.On<QueueDepthEventDto>("QueueDepthChanged", q =>
        {
            QueueDepths = new QueueDepthDto { StandardDepth = q.Standard, PriorityDepth = q.Priority };
            Notify();
        });

        hub.On<DeviceCommandDto>("CommandIssued", async cmd => await RefreshCommandDataAsync());
        hub.On<DeviceCommandDto>("CommandUndone", async cmd => await RefreshCommandDataAsync());

        return hub;
    }

    private void SetConnection(string state)
    {
        ConnectionState = state;
        Notify();
    }

    private void AddHeatCell(AnomalyCellDto cell)
    {
        if (!HeatCells.TryGetValue(cell.SensorId, out var row))
        {
            row = new List<AnomalyCellDto>();
            HeatCells[cell.SensorId] = row;
            HeatRows.Add(cell.SensorId);
        }
        row.Add(cell);
        if (row.Count > MaxHeatCells) row.RemoveAt(0);

        if (cell.Colour == "Red")
        {
            _activeCritical.RemoveAll(a => a.SensorId == cell.SensorId);
            _activeCritical.Insert(0, cell);
        }
    }

    private static async Task<T?> Try<T>(Func<Task<T?>> call) where T : class
    {
        try { return await call(); }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        try { if (_hub is not null) await _hub.DisposeAsync(); }
        catch { /* may never have connected; safe to ignore on teardown */ }
    }
}