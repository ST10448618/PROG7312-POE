namespace SmartX.Client.Models;

public class SensorDto
{
    public int Id { get; set; }
    public string MacAddress { get; set; } = "";
    public string Location { get; set; } = "";
    public string Category { get; set; } = "";
    public string Status { get; set; } = "Online";
    public List<SensorFileDto> Files { get; set; } = new();
}

public class AnomalyCellDto
{
    public string SensorId { get; set; } = "";
    public double Value { get; set; }
    public double Score { get; set; }
    public string Colour { get; set; } = "Grey";
    public DateTime Timestamp { get; set; }
}

public class SensorSummaryDto
{
    public int TotalSensors { get; set; }
    public int OnlineSensors { get; set; }
    public int TotalTelemetryLogs { get; set; }
    public Dictionary<string, int> ByCategory { get; set; } = new();
}

public class AnomalyLogDto { 
    public int Id { get; set; } 
    public string SensorId { get; set; } = ""; 
    public double Value { get; set; } 
    public double Score { get; set; } 
    public string Colour { get; set; } = ""; 
    public string Severity { get; set; } = ""; 
    public bool Acknowledged { get; set; } 
    public DateTime Timestamp { get; set; } 
}
public class IntegrationDto { 
    public int Id { get; set; } 
    public string Name { get; set; } = ""; 
    public string WebhookUrl { get; set; } = ""; 
    public string Status { get; set; } = "Pending"; 
    public DateTime? LastSyncAt { get; set; } 
}
public class SystemHealthDto { 
    public double UptimeSeconds { get; set; } 
    public bool DbConnected { get; set; } 
    public int TotalSensors { get; set; } 
    public int OnlineSensors { get; set; } 
    public int TotalTelemetryLogs { get; set; } 
    public int CriticalAnomaliesLast24h { get; set; } 
    public int LiveHubConnections { get; set; } 
}
public class TelemetryLogDto { 
    public string SensorId { get; set; } = ""; 
    public string RawValue { get; set; } = ""; 
    public string Unit { get; set; } = ""; 
    public DateTime Timestamp { get; set; } 
    }

public class SensorFileDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
}
