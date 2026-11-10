namespace SmartX.Api.Domain.Models;

public record MeshNode(string Id, string Label, double X, double Y, bool IsGateway = false);
public record MeshEdge(string From, string To, double Cost);
public readonly record struct MeshNeighbor(string Id, double Cost);