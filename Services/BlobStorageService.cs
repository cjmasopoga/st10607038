using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace SmartMonitoringSolutions.Services;

public class StorageOptions
{
    /// <summary>
    /// Full storage account connection string (account key based). Managed-identity/RBAC based
    /// access is not used because the deployment principal's Azure role does not permit granting
    /// Storage Blob/Table Data role assignments (Microsoft.Authorization/roleAssignments/write).
    /// </summary>
    public string? ConnectionString { get; set; }
    public string DroneImagesContainer { get; set; } = "drone-images";
    public string TelemetryTable { get; set; } = "TelemetryData";
}

/// <summary>
/// Uploads drone images / farm media directly to the "drone-images" Blob Storage container.
/// Uploading here triggers the BlobTrigger Azure Function which logs metadata to Table Storage.
/// </summary>
public class BlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly StorageOptions _options;

    public BlobStorageService(IOptions<StorageOptions> options)
    {
        _options = options.Value;
        _blobServiceClient = string.IsNullOrWhiteSpace(_options.ConnectionString)
            ? throw new InvalidOperationException("Storage:ConnectionString is not configured.")
            : new BlobServiceClient(_options.ConnectionString);
    }

    public async Task<string> UploadDroneImageAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_options.DroneImagesContainer);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobName = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}-{fileName}";
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(content, new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);

        return blobClient.Uri.ToString();
    }

    public async Task<IReadOnlyList<(string Name, Uri Uri, DateTimeOffset? LastModified, long Size)>> ListDroneImagesAsync(CancellationToken ct = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_options.DroneImagesContainer);
        if (!await containerClient.ExistsAsync(ct)) return [];

        var results = new List<(string, Uri, DateTimeOffset?, long)>();
        await foreach (var blob in containerClient.GetBlobsAsync(cancellationToken: ct))
        {
            var blobClient = containerClient.GetBlobClient(blob.Name);
            results.Add((blob.Name, blobClient.Uri, blob.Properties.LastModified, blob.Properties.ContentLength ?? 0));
        }
        return results.OrderByDescending(r => r.Item3).ToList();
    }
}
