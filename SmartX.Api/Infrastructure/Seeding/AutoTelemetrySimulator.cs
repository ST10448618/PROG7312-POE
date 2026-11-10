using Microsoft.EntityFrameworkCore;
using SmartX.Api.Infrastructure.Data;

namespace SmartX.Api.Infrastructure.Seeding;

public class AutoTelemetrySimulator : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<AutoTelemetrySimulator> _logger;
    private readonly Random _rnd = new();
    private readonly TimeSpan _tickInterval;

    public AutoTelemetrySimulator(IServiceScopeFactory scopeFactory, IHttpClientFactory httpFactory,
        IConfiguration config, ILogger<AutoTelemetrySimulator> logger)
    {
        _scopeFactory = scopeFactory;
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;

        // With a dozen mesh sensors, a 2 s tick means each one reports every ~25 s on average.
        _tickInterval = TimeSpan.FromMilliseconds(config.GetValue("Simulator:TickMs", 2000));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let Kestrel finish starting (and migrations/seeding finish) before the first request.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await SimulateOneTickAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogWarning(ex, "AutoTelemetrySimulator tick failed"); }

            await Task.Delay(_tickInterval, stoppingToken);
        }
    }

    private async Task SimulateOneTickAsync(CancellationToken ct)
    {
        List<string> macs;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            macs = await db.Sensors.AsNoTracking().Select(s => s.MacAddress).ToListAsync(ct);
        }
        if (macs.Count == 0) return;

        var mac = macs[_rnd.Next(macs.Count)];
        var roll = _rnd.NextDouble();

        var client = _httpFactory.CreateClient();
        client.BaseAddress = new Uri(_config["Simulator:BaseUrl"] ?? "http://localhost:8080");

        if (roll >= 0.95) // disconnect
        {
            await client.PostAsync($"/api/telemetry/{mac}/disconnect", null, ct);
            return;
        }

        var value = roll switch
        {
            < 0.75 => Math.Round(_rnd.NextDouble() * 100, 2),   // normal
            < 0.88 => 700 + _rnd.NextDouble() * 300,            // spike
            _ => -(700 + _rnd.NextDouble() * 300)               // drop
        };

        await client.PostAsJsonAsync("/api/telemetry/moisture", new { sensorId = mac, value = (float)value }, ct);
    }
}