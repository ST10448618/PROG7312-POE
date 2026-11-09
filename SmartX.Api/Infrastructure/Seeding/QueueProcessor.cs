using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Domain.Models;
using SmartX.Api.Hubs;
using SmartX.Api.Services;

namespace SmartX.Api.Infrastructure.Seeding;

public class QueueProcessor : BackgroundService
{
    private readonly CommandStreamService _cmds;
    private readonly IHubContext<TelemetryHub> _hub;
    private readonly ILogger<QueueProcessor> _logger;
    private readonly TimeSpan _standardCost;
    private readonly TimeSpan _priorityCost;

    public QueueProcessor(CommandStreamService cmds, IHubContext<TelemetryHub> hub,
        IConfiguration config, ILogger<QueueProcessor> logger)
    {
        _cmds = cmds;
        _hub = hub;
        _logger = logger;

        // Simulated per-item handling cost (downstream dispatch). It is what lets a backlog form in
        // the standard lane, so the priority bypass is visible. Set both to 0 for raw throughput.
        _standardCost = TimeSpan.FromMilliseconds(config.GetValue("QueueProcessor:StandardItemMs", 300));
        _priorityCost = TimeSpan.FromMilliseconds(config.GetValue("QueueProcessor:PriorityItemMs", 25));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _cmds.WaitForWorkAsync(stoppingToken);
                await DrainAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Queue processor iteration failed; continuing.");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }

    private async Task DrainAsync(CancellationToken ct)
    {
        while (_cmds.TryProcessNext(out var item))
        {
            var cost = item.Lane == QueueLane.Priority ? _priorityCost : _standardCost;
            if (cost > TimeSpan.Zero) await Task.Delay(cost, ct);

            await _hub.Clients.All.SendAsync("ItemProcessed", item, ct);
            await _hub.Clients.All.SendAsync("QueueDepthChanged", new
            {
                standard = _cmds.StandardQueueDepth,
                priority = _cmds.PriorityQueueDepth
            }, ct);
        }
    }
}