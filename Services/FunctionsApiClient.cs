using SmartMonitoringSolutions.Models;
using System.Net.Http.Json;

namespace SmartMonitoringSolutions.Services;

public class FunctionsApiOptions
{
    /// <summary>Base URL of the Azure Function App, e.g. https://func-agristream-xxxx.azurewebsites.net</summary>
    public string BaseUrl { get; set; } = string.Empty;
}

/// <summary>
/// HTTP client wrapper the MVC app uses to call the serverless data-processing pipeline:
/// GET /api/telemetry/{fieldId}      -> near-instant dashboard telemetry
/// POST /api/irrigation/trigger      -> enqueue an irrigation command
/// </summary>
public class FunctionsApiClient
{
    private readonly HttpClient _httpClient;

    public FunctionsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<TelemetryReadingDto>> GetLatestTelemetryAsync(string fieldId, int top = 20, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/api/telemetry/{Uri.EscapeDataString(fieldId)}?top={top}", ct);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<List<TelemetryReadingDto>>(cancellationToken: ct);
        return data ?? [];
    }

    public async Task<bool> TriggerIrrigationAsync(TriggerIrrigationViewModel command, CancellationToken ct = default)
    {
        var payload = new
        {
            deviceId = command.DeviceId,
            fieldId = command.FieldId,
            requestedBy = "web-app",
            reason = command.Reason
        };

        var response = await _httpClient.PostAsJsonAsync("/api/irrigation/trigger", payload, ct);
        return response.IsSuccessStatusCode;
    }
}
