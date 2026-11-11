namespace SmartX.Api.Domain.Models;

public readonly record struct TelemetryPoint(DateTime Timestamp, double Value, string Unit, string ValueType);