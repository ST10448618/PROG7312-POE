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

// ---------- graph analysis (BFS / DFS / Prim) ----------

public class MeshAnalysisDto
{
    public string GatewayId { get; set; } = "";
    public int NodeCount { get; set; }
    public List<string> DownNodes { get; set; } = new();
    public int ReachableCount { get; set; }
    public List<string> Stranded { get; set; } = new();
    public List<SubnetDto> Subnets { get; set; } = new();
    public MstDto Mst { get; set; } = new();
    public double ActiveLinkCost { get; set; }
    public double MstSavingsPercent { get; set; }
    public List<RouteChangeDto> Reroutes { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class SubnetDto
{
    public List<string> Members { get; set; } = new();
    public bool ContainsGateway { get; set; }
}

public class MstDto
{
    public List<MeshEdgeDto> Edges { get; set; } = new();
    public double TotalCost { get; set; }
    public int NodesReached { get; set; }
}

public class RouteChangeDto
{
    public string Id { get; set; } = "";
    public List<string> OldPath { get; set; } = new();
    public List<string>? NewPath { get; set; }
    public string? NextHop { get; set; }
    public double OldCost { get; set; }
    public double? NewCost { get; set; }
    public bool Stranded { get; set; }
}

public class RouteDto
{
    public string Id { get; set; } = "";
    public bool Reachable { get; set; }
    public List<string> Path { get; set; } = new();
    public double Cost { get; set; }
    public int Hops { get; set; }
}

public class RouteReportDto
{
    public string Id { get; set; } = "";
    public bool IsDown { get; set; }
    public RouteDto MstRoute { get; set; } = new();
    public RouteDto FewestHopsRoute { get; set; } = new();
    public bool RoutesDiffer { get; set; }
}

public class MeshHealthEventDto
{
    public int DownCount { get; set; }
    public List<string> DownNodes { get; set; } = new();
    public int StrandedCount { get; set; }
    public int ReachableCount { get; set; }
    public double MstCost { get; set; }
    public int Reroutes { get; set; }
}

// ---------- historical telemetry (AVL range queries) ----------

public class HistoryPointDto
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; } = "";
    public string ValueType { get; set; } = "";
}

public class HistoryResultDto
{
    public string SensorId { get; set; } = "";
    public int Total { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Average { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public List<HistoryPointDto> Points { get; set; } = new();
    public double ElapsedMicroseconds { get; set; }
    public int TreeSize { get; set; }
    public int TreeHeight { get; set; }
}

public class HistorySummaryDto
{
    public string SensorId { get; set; } = "";
    public int Count { get; set; }
    public DateTime? First { get; set; }
    public DateTime? Last { get; set; }
    public int TreeHeight { get; set; }
    public int MinimumPossibleHeight { get; set; }
    public bool IsBalanced { get; set; }
    public string? Problem { get; set; }
}

public class BenchmarkRowDto
{
    public int Size { get; set; }
    public int Queries { get; set; }
    public int WindowPoints { get; set; }
    public double AvlBuildMs { get; set; }
    public double LinearScanMicros { get; set; }
    public double AvlRangeMicros { get; set; }
    public double Speedup { get; set; }
    public int AvlHeight { get; set; }
    public int MinimumPossibleHeight { get; set; }
    public int? UnbalancedBstHeight { get; set; }
    public bool ResultsMatch { get; set; }
}

public class BenchmarkReportDto
{
    public List<BenchmarkRowDto> Rows { get; set; } = new();
    public string Method { get; set; } = "";
}