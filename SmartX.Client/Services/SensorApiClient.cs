using System.Net.Http.Json;
using SmartX.Client.Models;
using System.Text.Json;

namespace SmartX.Client.Services;

public class SensorApiClient
{
    private readonly HttpClient _http;
    public SensorApiClient(HttpClient http) => _http = http;

    public Task<List<SensorDto>?> GetSensorsAsync() => _http.GetFromJsonAsync<List<SensorDto>>("api/sensors");
    public Task<SensorSummaryDto?> GetSummaryAsync() => _http.GetFromJsonAsync<SensorSummaryDto>("api/sensors/summary");
    public Task<HttpResponseMessage> RegisterAsync(SensorDto sensor) => _http.PostAsJsonAsync("api/sensors", sensor);
    public Task<HttpResponseMessage> UploadFileAsync(string mac, MultipartFormDataContent content, string? category = null) =>
    _http.PostAsync($"api/sensors/{mac}/upload?category={Uri.EscapeDataString(category ?? "")}", content);
    public Task<List<TimelineBucketDto>?> GetTimelineAsync() => _http.GetFromJsonAsync<List<TimelineBucketDto>>("api/commands/timeline");

    public async Task<(bool Success, AnomalyCellDto? Result, string? Error)> PushMoistureAsync(string sensorId, float value)
    {
        var response = await _http.PostAsJsonAsync("api/telemetry/moisture", new { sensorId, value });
        if (response.IsSuccessStatusCode) return (true, await response.Content.ReadFromJsonAsync<AnomalyCellDto>(), null);
        return (false, null, await response.Content.ReadAsStringAsync());
    }

    public Task<HttpResponseMessage> ValidatePathAsync(string path) => _http.PostAsJsonAsync("api/deployment/validate", new { path });
    public Task<List<TelemetryLogDto>?> GetRecentTelemetryAsync() => _http.GetFromJsonAsync<List<TelemetryLogDto>>("api/telemetry/recent");
    public Task<double[][]?> GetSensorHistoryAsync(string sensorId) => _http.GetFromJsonAsync<double[][]>($"api/telemetry/history/{sensorId}");

public Task<List<AnomalyLogDto>?> GetAnomalyLogsAsync(string? severity, string? search, int pageSize = 25) =>
    _http.GetFromJsonAsync<List<AnomalyLogDto>>(
        $"api/anomalies?severity={Uri.EscapeDataString(severity ?? "")}&search={Uri.EscapeDataString(search ?? "")}&pageSize={pageSize}");
    public Task<HttpResponseMessage> AcknowledgeAnomalyAsync(int id) => _http.PostAsync($"api/anomalies/{id}/acknowledge", null);

    public Task<List<IntegrationDto>?> GetIntegrationsAsync() => _http.GetFromJsonAsync<List<IntegrationDto>>("api/integrations");
    public Task<HttpResponseMessage> RegisterIntegrationAsync(string name, string webhookUrl) =>
        _http.PostAsJsonAsync("api/integrations", new { name, webhookUrl });
    public Task<HttpResponseMessage> TestIntegrationAsync(int id) => _http.PostAsync($"api/integrations/{id}/test", null);
    public Task<HttpResponseMessage> DisconnectIntegrationAsync(int id) => _http.DeleteAsync($"api/integrations/{id}");

    public Task<SystemHealthDto?> GetHealthSummaryAsync() => _http.GetFromJsonAsync<SystemHealthDto>("api/system/health-summary");
    public Task<List<AnomalyLogDto>?> GetIncidentsAsync() => _http.GetFromJsonAsync<List<AnomalyLogDto>>("api/system/incidents");

    public Task<(bool Success, AnomalyCellDto? Result, string? Error)> PushExtremeAsync(string sensorId, float value)
    => PushMoistureAsync(sensorId, value);

    public async Task<HttpResponseMessage> MarkDisconnectedAsync(string sensorId)
        => await _http.PostAsync($"api/telemetry/{sensorId}/disconnect", null);

    public async Task<(bool Success, string? Message)> PushPowerAsync(string sensorId, int value)
{
    var response = await _http.PostAsJsonAsync("api/telemetry/power", new { sensorId, value });
    return response.IsSuccessStatusCode
        ? (true, await response.Content.ReadAsStringAsync())
        : (false, await response.Content.ReadAsStringAsync());
}

public async Task<(bool Success, string? Message)> PushValveAsync(string sensorId, bool value)
{
    var response = await _http.PostAsJsonAsync("api/telemetry/valve", new { sensorId, value });
    return response.IsSuccessStatusCode
        ? (true, await response.Content.ReadAsStringAsync())
        : (false, await response.Content.ReadAsStringAsync());
}

public Task<HttpResponseMessage> AggregatePowerAsync(double wattsA, double wattsB) =>
    _http.PostAsJsonAsync("api/power/aggregate", new
    {
        a = new { deviceId = "MeterA", watts = wattsA },
        b = new { deviceId = "MeterB", watts = wattsB }
    });

    public Task<HttpResponseMessage> UpdateSensorAsync(string mac, string location, string category) =>
    _http.PutAsJsonAsync($"api/sensors/{mac}", new { location, category });

    public Task<HttpResponseMessage> DeleteSensorAsync(string mac) =>
        _http.DeleteAsync($"api/sensors/{mac}");

    public Task<List<LiveDeviceStateDto>?> GetDeviceRegistryAsync() =>
    _http.GetFromJsonAsync<List<LiveDeviceStateDto>>("api/sensors/registry");

    public Task<QueueDepthDto?> GetQueueDepthsAsync() => _http.GetFromJsonAsync<QueueDepthDto>("api/commands/queues");
    public Task<List<DeviceCommandDto>?> GetCommandHistoryAsync() => _http.GetFromJsonAsync<List<DeviceCommandDto>>("api/commands/history");

    public Task<List<ProcessedReadingDto>?> GetProcessedLogAsync() =>
        _http.GetFromJsonAsync<List<ProcessedReadingDto>>("api/commands/processed?take=20");

    public Task<HttpResponseMessage> IssueCommandAsync(string targetMac, string commandType) =>
        _http.PostAsJsonAsync("api/commands/issue", new { targetMac, commandType });

    public Task<HttpResponseMessage> UndoLastCommandAsync() =>
        _http.PostAsync("api/commands/undo", null);
    public Task<List<string>?> GetActiveFaultsAsync() => _http.GetFromJsonAsync<List<string>>("api/commands/faults");

    public Task<List<SensorFileDto>?> GetSensorFilesAsync(string mac, string? category = null) =>
    _http.GetFromJsonAsync<List<SensorFileDto>>($"api/sensors/{mac}/files?category={Uri.EscapeDataString(category ?? "")}");
    
    // ---------- mesh, routing and history ----------

    public Task<MeshSnapshotDto?> GetMeshTopologyAsync() =>
        _http.GetFromJsonAsync<MeshSnapshotDto>("api/mesh/topology");

    public Task<(MeshAnalysisDto? Value, string? Error)> GetMeshAnalysisAsync(IEnumerable<string>? assumeDown = null) =>
        TryGetAsync<MeshAnalysisDto>("api/mesh/analysis" + AssumeQuery(assumeDown));

    public Task<(RouteReportDto? Value, string? Error)> GetRouteAsync(string mac, IEnumerable<string>? assumeDown = null) =>
        TryGetAsync<RouteReportDto>($"api/mesh/route/{Uri.EscapeDataString(mac)}" + AssumeQuery(assumeDown));

    public Task<(HistoryResultDto? Value, string? Error)> QueryHistoryAsync(
        string mac, DateTime fromUtc, DateTime toUtc, int skip, int take, bool descending)
    {
        var from = Uri.EscapeDataString(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc).ToString("o"));
        var to = Uri.EscapeDataString(DateTime.SpecifyKind(toUtc, DateTimeKind.Utc).ToString("o"));
        return TryGetAsync<HistoryResultDto>(
            $"api/history/{Uri.EscapeDataString(mac)}?from={from}&to={to}&skip={skip}&take={take}&order={(descending ? "desc" : "asc")}");
    }

    public Task<(HistorySummaryDto? Value, string? Error)> GetHistorySummaryAsync(string mac) =>
        TryGetAsync<HistorySummaryDto>($"api/history/{Uri.EscapeDataString(mac)}/summary");

    public Task<(BenchmarkReportDto? Value, string? Error)> GetBenchmarkAsync(string sizes = "1000,10000,100000") =>
        TryGetAsync<BenchmarkReportDto>($"api/history/benchmark?sizes={Uri.EscapeDataString(sizes)}");

    private static string AssumeQuery(IEnumerable<string>? ids)
    {
        var list = ids?.ToList();
        return list is null || list.Count == 0 ? "" : "?assumeDown=" + Uri.EscapeDataString(string.Join(",", list));
    }

    /// <summary>GET that turns failures into a readable message instead of throwing.</summary>
    private async Task<(T? Value, string? Error)> TryGetAsync<T>(string url) where T : class
    {
        try
        {
            var response = await _http.GetAsync(url);
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<T>(), null);
            return (null, await ReadErrorAsync(response));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return (null, "Could not reach the API.");
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                foreach (var key in new[] { "detail", "message" })
                    if (doc.RootElement.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
                        return p.GetString()!;
        }
        catch (JsonException) { /* not JSON; fall through */ }
        return $"Request failed ({(int)response.StatusCode}).";
    }

    public Task<InsightsSnapshotDto?> GetInsightsAsync() =>
    _http.GetFromJsonAsync<InsightsSnapshotDto>("api/insights");

    public Task<HttpResponseMessage> RecordSearchAsync(string text, string category, string state) =>
    _http.PostAsJsonAsync("api/insights/search", new { text, category, state });
}