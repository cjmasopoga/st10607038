using System.ComponentModel.DataAnnotations;

namespace SmartMonitoringSolutions.Models;

/// <summary>
/// Master registry of field devices (irrigation controllers, sensor gateways, etc.).
/// Updated by the Queue-triggered Azure Function after an irrigation command is processed.
/// Stored in the "DeviceRegistry" table in Azure SQL Database.
/// </summary>
public class DeviceRegistry
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string DeviceId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string DeviceType { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Status { get; set; } = "Unknown";

    [MaxLength(256)]
    public string? LastCommand { get; set; }

    public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
