using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SmartMonitoringSolutions.Functions.Models;

namespace SmartMonitoringSolutions.Functions.Functions;

/// <summary>
/// Blob-triggered function. Fires whenever a drone image (or other farm media) is uploaded
/// to the "drone-images" Blob Storage container. Writes a metadata entry directly into
/// Azure Table Storage ("DroneImageLog") so the web app / dashboard can list recent uploads
/// without re-scanning the blob container.
/// </summary>
public class DroneImageBlobTrigger
{
    private readonly ILogger<DroneImageBlobTrigger> _logger;

    public DroneImageBlobTrigger(ILogger<DroneImageBlobTrigger> logger)
    {
        _logger = logger;
    }

    [Function("DroneImageBlobTrigger")]
    [TableOutput("DroneImageLog", Connection = "AzureWebJobsStorage")]
    public DroneImageLogEntity Run(
        [BlobTrigger("drone-images/{name}", Connection = "AzureWebJobsStorage")] Stream blobStream,
        string name,
        Microsoft.Azure.Functions.Worker.FunctionContext context)
    {
        _logger.LogInformation("Drone image uploaded: {BlobName} ({Size} bytes)", name, blobStream.Length);

        var now = DateTimeOffset.UtcNow;
        var entity = new DroneImageLogEntity
        {
            // Partition by upload date so lookups/scans stay bounded as volume grows.
            PartitionKey = now.ToString("yyyy-MM-dd"),
            RowKey = $"{now.Ticks:D19}-{name}",
            BlobName = name,
            BlobUrl = GetBlobUrl(name),
            SizeBytes = blobStream.Length,
            ContentType = "image/*",
            UploadedAt = now
        };

        return entity;
    }

    private static string GetBlobUrl(string blobName)
    {
        var blobServiceUri = Environment.GetEnvironmentVariable("AzureWebJobsStorage__blobServiceUri");
        return blobServiceUri is null
            ? blobName
            : $"{blobServiceUri.TrimEnd('/')}/drone-images/{blobName}";
    }
}
