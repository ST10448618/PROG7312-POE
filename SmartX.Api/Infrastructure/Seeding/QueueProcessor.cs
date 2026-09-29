using SmartX.Api.Services;

namespace SmartX.Api.Infrastructure.Seeding;

public class QueueProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    public QueueProcessor(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var cmds = scope.ServiceProvider.GetRequiredService<CommandStreamService>();

            // Priority items drain first — that's the entire point of the structure.
            cmds.DequeuePriority();
            cmds.DequeueStandard();

            await Task.Delay(Interval, stoppingToken);
        }
    }
}