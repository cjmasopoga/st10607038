using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;

namespace SmartMonitoringSolutions.Services;

/// <summary>Mirrors SmartMonitoringSolutions.Functions.Models.TelemetryEntity (shared Table Storage schema).</summary>
public class TelemetryTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string DeviceId { get; set; } = string.Empty;
    public string ReadingType { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
}

/// <summary>
/// Writes soil-sensor / environmental telemetry readings directly into Azure Table Storage.
/// In production, the millions of field sensors write here directly (or via an edge gateway);
/// this service exposes a "Simulate Sensor Reading" action for demo/testing purposes. Reads are
/// served near-instantly via the GetTelemetry Azure Function rather than round-tripping through
/// this service.
/// </summary>
public class TelemetryTableService
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly StorageOptions _options;

    public TelemetryTableService(IOptions<StorageOptions> options)
    {
        _options = options.Value;
        _tableServiceClient = string.IsNullOrWhiteSpace(_options.ConnectionString)
            ? throw new InvalidOperationException("Storage:ConnectionString is not configured.")
            : new TableServiceClient(_options.ConnectionString);
    }

    public async Task WriteReadingAsync(string fieldId, string deviceId, string readingType, double value, string unit, CancellationToken ct = default)
    {
        var tableClient = _tableServiceClient.GetTableClient(_options.TelemetryTable);
        await tableClient.CreateIfNotExistsAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var entity = new TelemetryTableEntity
        {
            // Inverted ticks RowKey => ascending RowKey order == most-recent-first (matches GetTelemetry Function).
            PartitionKey = fieldId,
            RowKey = $"{DateTimeOffset.MaxValue.Ticks - now.Ticks:D19}",
            DeviceId = deviceId,
            ReadingType = readingType,
            Value = value,
            Unit = unit,
            RecordedAt = now
        };

        await tableClient.AddEntityAsync(entity, ct);
    }
}
