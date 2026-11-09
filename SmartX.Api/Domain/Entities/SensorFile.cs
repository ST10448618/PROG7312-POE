using SmartX.Api.Domain.ValueObjects;

namespace SmartX.Api.Domain.Entities;

public class SensorFile
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public string Category { get; set; } = FileCategory.Other;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public int SensorProfileId { get; set; }
    public SensorProfile? SensorProfile { get; set; }
}