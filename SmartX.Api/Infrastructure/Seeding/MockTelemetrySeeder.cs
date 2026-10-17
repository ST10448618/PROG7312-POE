using Microsoft.EntityFrameworkCore;
using SmartX.Api.Domain.Collections;
using SmartX.Api.Domain.Entities;
using SmartX.Api.Infrastructure.Data;
using SmartX.Api.Services;

namespace SmartX.Api.Infrastructure.Seeding;

public class MockTelemetrySeeder : IHostedService
{
    private readonly IServiceProvider _services;
    public MockTelemetrySeeder(IServiceProvider services) => _services = services;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var batchStore = scope.ServiceProvider.GetRequiredService<TelemetryBatchStore>();
        var anomaly = scope.ServiceProvider.GetRequiredService<AnomalyDetectionService>();

        if (await db.Sensors.AnyAsync(cancellationToken)) return;

        var demoSensors = new[]
        {
            new SensorProfile { MacAddress = "AA:BB:CC:00:01:01", Location = "Facility A/Zone 1/Sub-Zone B/Rack 1", Category = "Environmental" },
            new SensorProfile { MacAddress = "AA:BB:CC:00:01:02", Location = "Facility A/Zone 1/Sub-Zone C", Category = "Power Consumption" },
            new SensorProfile { MacAddress = "AA:BB:CC:00:01:03", Location = "Facility A/Zone 1/Sub-Zone B/Rack 1", Category = "Actuator" }
        };
        db.Sensors.AddRange(demoSensors);
        await db.SaveChangesAsync(cancellationToken);

        var registry = scope.ServiceProvider.GetRequiredService<LiveDeviceRegistry>();
        foreach (var s in demoSensors)
        registry.Register(s.MacAddress, s.Location, s.Category);

        var rnd = new Random();
        foreach (var sensor in demoSensors)
        {
            batchStore.SeedMockData(sensor.MacAddress, 50);
            for (var i = 0; i < 20; i++)
                anomaly.Score(sensor.MacAddress, Math.Round(rnd.NextDouble() * 100, 2));
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}