using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Hubs;

namespace SmartX.Api.Services;

public class MeshHealthMonitor : BackgroundService
{
    private readonly MeshService _mesh;
    private readonly MeshAnalysisService _analysis;
    private readonly IHubContext<TelemetryHub> _hub;
    private readonly ILogger<MeshHealthMonitor> _logger;

    public MeshHealthMonitor(MeshService mesh, MeshAnalysisService analysis,
        IHubContext<TelemetryHub> hub, ILogger<MeshHealthMonitor> logger)
    {
        _mesh = mesh;
        _analysis = analysis;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string? last = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                if (!_mesh.IsReady) continue;

                var a = _analysis.Analyze();
                var signature = string.Join('|', a.DownNodes.Concat(a.Stranded.Select(s => "x:" + s)));

                if (last is null) { last = signature; continue; } // first pass only sets the baseline
                if (signature == last) continue;
                last = signature;

                await _hub.Clients.All.SendAsync("MeshHealthChanged", new
                {
                    downCount = a.DownNodes.Count,
                    downNodes = a.DownNodes,
                    strandedCount = a.Stranded.Count,
                    reachableCount = a.ReachableCount,
                    mstCost = a.Mst.TotalCost,
                    reroutes = a.Reroutes.Count,
                    at = a.GeneratedAt
                }, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Mesh health check failed; will retry.");
            }
        }
    }
}