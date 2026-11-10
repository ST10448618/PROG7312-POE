namespace SmartX.Client.Models;

public class MeshSnapshotDto
{
    public string GatewayId { get; set; } = "";
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
    public int DownCount { get; set; }
    public List<MeshNodeDto> Nodes { get; set; } = new();
    public List<MeshEdgeDto> Edges { get; set; } = new();
}

public class MeshNodeDto
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public bool IsGateway { get; set; }
    public string Category { get; set; } = "";
    public string Location { get; set; } = "";
    public string Status { get; set; } = "";
    public bool IsDown { get; set; }
    public int Degree { get; set; }
    public double? LastValue { get; set; }
}

public class MeshEdgeDto
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public double Cost { get; set; }
    public bool Active { get; set; }
}