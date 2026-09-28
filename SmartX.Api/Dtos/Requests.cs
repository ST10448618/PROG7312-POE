using SmartX.Api.Domain.ValueObjects;

namespace SmartX.Api.Dtos;

public record RegisterSensorRequest(string MacAddress, string Location, string Category);
public record SensorStatusRequest(string Status);
public record MoistureReadingRequest(string SensorId, float Value);
public record PowerReadingRequest(string SensorId, int Value);
public record ValveReadingRequest(string SensorId, bool Value);
public record PowerAggregateRequest(PowerReading A, PowerReading B);
public record DeploymentPathRequest(string Path);
public record UpdateSensorRequest(string Location, string Category);
public record IssueCommandRequest(string TargetMac, string CommandType);