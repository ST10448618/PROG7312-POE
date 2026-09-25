using System.Net.Http.Json;
using SmartX.Client.Models;

namespace SmartX.Client.Services;

public class SensorApiClient
{
    private readonly HttpClient _http;
    public SensorApiClient(HttpClient http) => _http = http;

    public Task<List<SensorDto>?> GetSensorsAsync() => _http.GetFromJsonAsync<List<SensorDto>>("api/sensors");
    public Task<SensorSummaryDto?> GetSummaryAsync() => _http.GetFromJsonAsync<SensorSummaryDto>("api/sensors/summary");
    public Task<HttpResponseMessage> RegisterAsync(SensorDto sensor) => _http.PostAsJsonAsync("api/sensors", sensor);
    public Task<HttpResponseMessage> UploadFileAsync(string mac, MultipartFormDataContent content) => _http.PostAsync($"api/sensors/{mac}/upload", content);

    public async Task<(bool Success, AnomalyCellDto? Result, string? Error)> PushMoistureAsync(string sensorId, float value)
    {
        var response = await _http.PostAsJsonAsync("api/telemetry/moisture", new { sensorId, value });
        if (response.IsSuccessStatusCode) return (true, await response.Content.ReadFromJsonAsync<AnomalyCellDto>(), null);
        return (false, null, await response.Content.ReadAsStringAsync());
    }

    public Task<HttpResponseMessage> ValidatePathAsync(string path) => _http.PostAsJsonAsync("api/deployment/validate", new { path });
    public Task<List<TelemetryLogDto>?> GetRecentTelemetryAsync() => _http.GetFromJsonAsync<List<TelemetryLogDto>>("api/telemetry/recent");
    public Task<double[][]?> GetSensorHistoryAsync(string sensorId) => _http.GetFromJsonAsync<double[][]>($"api/telemetry/history/{sensorId}");

    public Task<List<AnomalyLogDto>?> GetAnomalyLogsAsync(string? severity, string? search) =>
        _http.GetFromJsonAsync<List<AnomalyLogDto>>($"api/anomalies?severity={severity}&search={search}");
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
}