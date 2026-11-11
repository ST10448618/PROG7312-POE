namespace SmartX.Api.Domain.Models;

public record MeshRoute(string Id, bool Reachable, List<string> Path, double Cost, int Hops);

public record MstResult(List<MeshEdge> Edges, double TotalCost, Dictionary<string, string> Parent, HashSet<string> Reached);

public record SubnetGroup(List<string> Members, bool ContainsGateway);

public record RouteChange(
    string Id, List<string> OldPath, List<string>? NewPath, string? NextHop,
    double OldCost, double? NewCost, bool Stranded);