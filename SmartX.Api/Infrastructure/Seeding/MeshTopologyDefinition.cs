using SmartX.Api.Domain.Collections;
using SmartX.Api.Domain.Models;

namespace SmartX.Api.Infrastructure.Seeding;

public record MeshSensorSpec(string Mac, string Location, string Category, double X, double Y);

public static class MeshTopologyDefinition
{
    public const string GatewayId = "AA:BB:CC:00:00:01";

    private static string Mac(string suffix) => $"AA:BB:CC:00:01:{suffix}";
    
    public static readonly IReadOnlyList<MeshSensorSpec> Sensors = new List<MeshSensorSpec>
    {
        new(Mac("01"), "Facility A/Zone 1/Sub-Zone B/Rack 1", "Environmental",     46, 12),
        new(Mac("02"), "Facility A/Zone 1/Sub-Zone C",        "Power Consumption", 26, 24),
        new(Mac("03"), "Facility A/Zone 1/Sub-Zone B/Rack 1", "Actuator",          66, 25),
        new(Mac("04"), "Facility A/Zone 1/Sub-Zone B/Rack 2", "Environmental",     46, 38),
        new(Mac("05"), "Facility A/Zone 1/Sub-Zone B/Rack 2", "Power Consumption", 68, 50),
        new(Mac("06"), "Facility A/Zone 2/Sub-Zone A/Rack 1", "Environmental",     47, 62),
        new(Mac("07"), "Facility A/Zone 2/Sub-Zone A/Rack 1", "Actuator",          66, 75),
        new(Mac("08"), "Facility A/Zone 2/Sub-Zone B",        "Environmental",     46, 88),
        new(Mac("09"), "Facility A/Zone 2/Sub-Zone C",        "Power Consumption", 27, 50),
        new(Mac("0A"), "Facility A/Zone 3/Sub-Zone A/Rack 1", "Environmental",     26, 76),
        new(Mac("0B"), "Facility A/Zone 3/Sub-Zone A/Rack 2", "Actuator",           8, 36),
        new(Mac("0C"), "Facility A/Zone 3/Sub-Zone B",        "Power Consumption",  8, 64),
    };

    private static readonly (string A, string B, double Cost)[] Links =
    {
        // gateway and its direct neighbours
        (GatewayId, Mac("03"), 3.0), (GatewayId, Mac("05"), 2.0), (GatewayId, Mac("07"), 3.2),
        // inner ring
        (Mac("03"), Mac("05"), 2.4), (Mac("05"), Mac("07"), 2.6),
        (Mac("03"), Mac("01"), 3.5), (Mac("03"), Mac("04"), 2.8),
        (Mac("05"), Mac("06"), 2.2), (Mac("07"), Mac("06"), 2.9), (Mac("07"), Mac("08"), 3.4),
        // middle layer
        (Mac("01"), Mac("04"), 2.5), (Mac("06"), Mac("08"), 2.7),
        (Mac("04"), Mac("02"), 3.0), (Mac("04"), Mac("09"), 3.3),
        (Mac("06"), Mac("09"), 2.4), (Mac("06"), Mac("0A"), 3.5), (Mac("08"), Mac("0A"), 2.8),
        // outer layer
        (Mac("02"), Mac("09"), 2.6), (Mac("09"), Mac("0A"), 2.5),
        (Mac("02"), Mac("0B"), 3.1), (Mac("09"), Mac("0C"), 3.0),
        (Mac("0B"), Mac("0C"), 2.2), (Mac("0A"), Mac("0C"), 2.7),
    };

    public static MeshGraph Build()
    {
        var graph = new MeshGraph();
        graph.AddNode(new MeshNode(GatewayId, "GW", 86, 50, IsGateway: true));

        foreach (var s in Sensors)
            graph.AddNode(new MeshNode(s.Mac, s.Mac[^2..], s.X, s.Y));

        foreach (var (a, b, cost) in Links)
            graph.AddEdge(a, b, cost);

        return graph;
    }
}