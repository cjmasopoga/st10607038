using System.ComponentModel.DataAnnotations;

namespace SmartMonitoringSolutions.Models;

public class LoginViewModel
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class RegisterUserViewModel
{
    [Required, MaxLength(64)]
    public string Username { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public UserRole Role { get; set; } = UserRole.Farmer;
}

/// <summary>A single telemetry reading as returned by the GetTelemetry Azure Function.</summary>
public class TelemetryReadingDto
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string ReadingType { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
}

public class DashboardViewModel
{
    [Required, MaxLength(64)]
    public string FieldId { get; set; } = "field-01";
    public List<TelemetryReadingDto> Readings { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class SimulateTelemetryViewModel
{
    [Required] public string FieldId { get; set; } = "field-01";
    [Required] public string DeviceId { get; set; } = "sensor-001";
    [Required] public string ReadingType { get; set; } = "SoilMoisture";
    [Required] public double Value { get; set; } = 42.5;
    [Required] public string Unit { get; set; } = "%";
}

public class TriggerIrrigationViewModel
{
    [Required] public string DeviceId { get; set; } = string.Empty;
    [Required] public string FieldId { get; set; } = string.Empty;
    public string Reason { get; set; } = "Manually triggered from dashboard";
}

public class MediaUploadViewModel
{
    [Required]
    public IFormFile? File { get; set; }
}
