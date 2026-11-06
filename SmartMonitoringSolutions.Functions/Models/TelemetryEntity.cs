using Azure;
using Azure.Data.Tables;

namespace SmartMonitoringSolutions.Functions.Models;

/// <summary>
/// Represents a single soil sensor / environmental telemetry reading stored in Azure Table Storage.
/// PartitionKey = FieldId (groups readings by farm field), RowKey = reverse-chronological ticks
/// so the most recent readings sort first within a partition query.
/// </summary>
public class TelemetryEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string DeviceId { get; set; } = string.Empty;
    public string ReadingType { get; set; } = string.Empty; // e.g. SoilMoisture, Temperature, IrrigationEvent
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
}

/// <summary>
/// Metadata entry written to Table Storage whenever a drone image is uploaded to Blob Storage.
/// </summary>
public class DroneImageLogEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string BlobName { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; }
}
