using Microsoft.EntityFrameworkCore;
using SmartX.Api.Domain.Entities;
using SmartX.Api.Infrastructure.Data;
using SmartX.Api.Services;

namespace SmartX.Api.Infrastructure.Seeding;

public class MeshTopologySeeder : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly MeshService _mesh;
    private readonly ILogger<MeshTopologySeeder> _logger;

    public MeshTopologySeeder(IServiceProvider services, MeshService mesh, ILogger<MeshTopologySeeder> logger)
    {
        _services = services;
        _mesh = mesh;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var registry = scope.ServiceProvider.GetRequiredService<LiveDeviceRegistry>();

        var existing = await db.Sensors.AsNoTracking().Select(s => s.MacAddress).ToListAsync(cancellationToken);
        var known = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var missing = MeshTopologyDefinition.Sensors.Where(s => !known.Contains(s.Mac)).ToList();
        if (missing.Count > 0)
        {
            db.Sensors.AddRange(missing.Select(s => new SensorProfile
            {
                MacAddress = s.Mac,
                Location = s.Location,
                Category = s.Category
            }));
            await db.SaveChangesAsync(cancellationToken);

            foreach (var s in missing)
                registry.Register(s.Mac, s.Location, s.Category);
        }

        var graph = MeshTopologyDefinition.Build();
        _mesh.SetGraph(graph);

        _logger.LogInformation("Mesh topology loaded: {Nodes} nodes, {Edges} links, {Added} sensors added.",
            graph.NodeCount, graph.EdgeCount, missing.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}