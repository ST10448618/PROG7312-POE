using System.Globalization;
using SmartX.Client.Models;

namespace SmartX.Client.Services;

public partial class TelemetryStore
{
    private static readonly TimeSpan MeshRefreshInterval = TimeSpan.FromSeconds(2);
    public const int HistPageSize = 25;

    private bool _meshWanted;
    private bool _meshRefreshQueued;
    private DateTime _lastMeshRefresh = DateTime.MinValue;

    // ---------- mesh ----------
    public MeshSnapshotDto? Mesh { get; private set; }
    public MeshAnalysisDto? MeshAnalysis { get; private set; }
    public string? MeshError { get; private set; }
    public DateTime? MeshHealthChangedAt { get; private set; }

    // ---------- topology UI state ----------
    public string TopoSelectedMac { get; private set; } = "";
    public string TopoSearch { get; set; } = "";
    public bool TopoShowMst { get; set; } = true;
    public bool TopoWhatIf { get; private set; }
    public HashSet<string> TopoAssumedDown { get; } = new(StringComparer.OrdinalIgnoreCase);
    public RouteReportDto? TopoRoute { get; private set; }
    public string? TopoRouteError { get; private set; }

    // ---------- history query state ----------
    public string HistMac { get; set; } = "";
    public string HistFrom { get; set; } = "";   // datetime-local text, browser local time
    public string HistTo { get; set; } = "";
    public bool HistDescending { get; set; }
    public int HistPage { get; set; }
    public bool HistLoading { get; private set; }
    public HistoryResultDto? HistResult { get; private set; }
    public HistorySummaryDto? HistSummary { get; private set; }
    public string? HistError { get; private set; }

    // ---------- benchmark ----------
    public BenchmarkReportDto? Benchmark { get; private set; }
    public bool BenchRunning { get; private set; }
    public string? BenchError { get; private set; }

    // ================================================================ mesh

    /// <summary>Called by the Topology page on open.</summary>
    public async Task EnsureTopologyAsync()
    {
        EnsureHistoryDefaults();
        await ReloadTopologyViewAsync();
        if (!string.IsNullOrEmpty(HistMac) && HistResult is null) await RunHistoryQueryAsync();
    }

    private async Task ReloadTopologyViewAsync()
    {
        await RefreshMeshAsync();
        if (!string.IsNullOrEmpty(TopoSelectedMac) && !IsGatewayId(TopoSelectedMac))
            await LoadRouteAsync(TopoSelectedMac);
    }

    public async Task RefreshMeshAsync()
    {
        _meshWanted = true;
        var assumed = TopoWhatIf ? TopoAssumedDown.ToList() : new List<string>();

        var mesh = await Try<MeshSnapshotDto>(() => _api.GetMeshTopologyAsync());
        var (analysis, error) = await _api.GetMeshAnalysisAsync(assumed);

        if (mesh is not null) Mesh = mesh;
        if (analysis is not null) MeshAnalysis = analysis;
        MeshError = mesh is null || analysis is null
            ? error ?? "The mesh is still loading or the API is unreachable. Showing the last known state."
            : null;

        _lastMeshRefresh = DateTime.UtcNow;
        Notify();
    }

    private void OnMeshHealthChanged(MeshHealthEventDto e)
    {
        MeshHealthChangedAt = DateTime.UtcNow;
        if (_meshWanted) _ = ReloadTopologyViewAsync();
    }

    /// <summary>Registry updates arrive every few seconds; refresh the mesh at most once per interval.</summary>
    private void QueueMeshRefresh()
    {
        if (!_meshWanted || _meshRefreshQueued) return;
        _meshRefreshQueued = true;
        _ = RunQueuedMeshRefreshAsync();
    }

    private async Task RunQueuedMeshRefreshAsync()
    {
        try
        {
            var wait = MeshRefreshInterval - (DateTime.UtcNow - _lastMeshRefresh);
            if (wait > TimeSpan.Zero) await Task.Delay(wait);
            await ReloadTopologyViewAsync();
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); }
        finally { _meshRefreshQueued = false; }
    }

    // ============================================================ selection

    public async Task SelectNodeAsync(string mac)
    {
        if (Eq(TopoSelectedMac, mac))
        {
            TopoSelectedMac = "";
            TopoRoute = null;
            TopoRouteError = null;
            Notify();
            return;
        }
        await SetSelectionAsync(mac);
    }

    private async Task SetSelectionAsync(string mac)
    {
        TopoSelectedMac = mac;
        TopoRoute = null;
        TopoRouteError = null;

        if (IsGatewayId(mac)) { Notify(); return; } // the gateway is the destination: no route, no sensor history

        await LoadRouteAsync(mac);
        HistMac = mac;
        HistPage = 0;
        await RunHistoryQueryAsync();
    }

    /// <summary>Route lookup by full MAC or short id (the two-character label, e.g. 0C).</summary>
    public async Task TraceAsync(string text)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0)
        {
            TopoRoute = null;
            TopoRouteError = "Enter a MAC address or a short id such as 0C.";
            Notify();
            return;
        }

        var node = Mesh?.Nodes.FirstOrDefault(n => Eq(n.Id, text))
                   ?? Mesh?.Nodes.FirstOrDefault(n => !n.IsGateway && Eq(n.Label, text));
        if (node is null)
        {
            TopoRoute = null;
            TopoRouteError = $"No node in the mesh matches '{text}'.";
            Notify();
            return;
        }

        TopoSearch = node.Id;
        await SetSelectionAsync(node.Id);
    }

    public async Task LoadRouteAsync(string mac)
    {
        var (report, error) = await _api.GetRouteAsync(mac, TopoWhatIf ? TopoAssumedDown.ToList() : null);
        TopoRoute = report;
        TopoRouteError = report is null ? error : null;
        Notify();
    }

    // ============================================================= what-if

    public async Task SetWhatIfAsync(bool on)
    {
        TopoWhatIf = on;
        if (!on) TopoAssumedDown.Clear();
        await ReloadTopologyViewAsync();
    }

    public async Task ToggleAssumedDownAsync(string mac)
    {
        if (!TopoAssumedDown.Add(mac)) TopoAssumedDown.Remove(mac);
        await ReloadTopologyViewAsync();
    }

    public async Task ClearAssumedDownAsync()
    {
        TopoAssumedDown.Clear();
        await ReloadTopologyViewAsync();
    }

    // ============================================================= history

    private void EnsureHistoryDefaults()
    {
        if (!string.IsNullOrEmpty(HistFrom) && !string.IsNullOrEmpty(HistTo)) return;
        ApplyRange(TimeSpan.FromHours(6));
    }

    private void ApplyRange(TimeSpan span)
    {
        var now = DateTime.Now;
        HistTo = FormatLocal(now.AddMinutes(1));
        HistFrom = FormatLocal(now - span);
    }

    public async Task SetHistoryRangeAsync(TimeSpan span)
    {
        ApplyRange(span);
        HistPage = 0;
        await RunHistoryQueryAsync();
    }

    public async Task RunHistoryQueryAsync()
    {
        if (string.IsNullOrWhiteSpace(HistMac))
        {
            HistError = "Select a sensor first.";
            Notify();
            return;
        }
        if (!TryParseLocal(HistFrom, out var from) || !TryParseLocal(HistTo, out var to))
        {
            HistError = "Pick a valid start and end time.";
            Notify();
            return;
        }
        if (from > to)
        {
            HistError = "The start time must not be after the end time.";
            Notify();
            return;
        }

        HistLoading = true;
        Notify();

        var (result, error) = await _api.QueryHistoryAsync(HistMac, from, to, HistPage * HistPageSize, HistPageSize, HistDescending);
        var (summary, _) = await _api.GetHistorySummaryAsync(HistMac);

        HistResult = result;
        HistSummary = summary;
        HistError = result is null ? error : null;
        HistLoading = false;
        Notify();
    }

    // ========================================================== benchmark

    public async Task RunBenchmarkAsync()
    {
        if (BenchRunning) return;
        BenchRunning = true;
        BenchError = null;
        Notify();

        var (report, error) = await _api.GetBenchmarkAsync();
        if (report is not null) Benchmark = report;
        BenchError = report is null ? error : null;
        BenchRunning = false;
        Notify();
    }

    // ============================================================= helpers

    private bool IsGatewayId(string mac) => Mesh?.Nodes.Any(n => n.IsGateway && Eq(n.Id, mac)) == true;

    private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string FormatLocal(DateTime local) => local.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    private static bool TryParseLocal(string text, out DateTime utc)
    {
        utc = default;
        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var local)) return false;
        utc = local.ToUniversalTime();
        return true;
    }
}