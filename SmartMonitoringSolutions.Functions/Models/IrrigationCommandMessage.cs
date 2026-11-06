using System.Text.Json.Serialization;

namespace SmartMonitoringSolutions.Functions.Models;

/// <summary>Message body placed on / read from the "irrigation-commands" Azure Storage Queue.</summary>
public class IrrigationCommandMessage
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("fieldId")]
    public string FieldId { get; set; } = string.Empty;

    [JsonPropertyName("requestedBy")]
    public string RequestedBy { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("requestedAtUtc")]
    public DateTimeOffset RequestedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
