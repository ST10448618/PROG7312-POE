using SmartX.Api.Services;

namespace SmartX.Api.Infrastructure.Seeding;
public class PatternHistorySeeder : IHostedService
{
    private readonly PredictiveActionEngine _engine;
    public PatternHistorySeeder(PredictiveActionEngine engine) => _engine = engine;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        const string moisture = "AA:BB:CC:00:01:01";
        const string power = "AA:BB:CC:00:01:02";
        const string pump = "AA:BB:CC:00:01:03";

        _engine.SeedAssociation($"Critical:{moisture}", $"Command:TriggerPump:{pump}", 6);
        _engine.SeedAssociation($"Critical:{moisture}", $"Command:ShutdownValve:{pump}", 1);
        _engine.SeedAssociation($"Warning:{moisture}", $"Command:TriggerPump:{pump}", 3);
        _engine.SeedAssociation($"Critical:{power}", $"Command:ToggleRelay:{pump}", 4);
        _engine.SeedAssociation($"Warning:{power}", $"Command:ToggleRelay:{pump}", 3);
        _engine.SeedAssociation($"Disconnected:{power}", "Search:Power Consumption|Faulted|", 3);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}