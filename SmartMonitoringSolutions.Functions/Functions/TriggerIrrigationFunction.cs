using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SmartMonitoringSolutions.Functions.Models;
using System.Net;
using System.Text.Json;

namespace SmartMonitoringSolutions.Functions.Functions;

/// <summary>Multi-output result: an HTTP response plus (optionally) a queue message.</summary>
public class TriggerIrrigationResult
{
    [QueueOutput("irrigation-commands", Connection = "AzureWebJobsStorage")]
    public string? QueueMessage { get; set; }

    [HttpResult]
    public HttpResponseData HttpResponse { get; set; } = null!;
}

/// <summary>
/// HTTP POST trigger. Called by the MVC web app when an automated sensor detects a problem
/// or a user clicks "Trigger Irrigation". Places an irrigation command message onto the
/// "irrigation-commands" Azure Storage Queue for asynchronous processing.
/// Route: POST /api/irrigation/trigger
/// </summary>
public class TriggerIrrigationFunction
{
    private readonly ILogger<TriggerIrrigationFunction> _logger;

    public TriggerIrrigationFunction(ILogger<TriggerIrrigationFunction> logger)
    {
        _logger = logger;
    }

    [Function("TriggerIrrigation")]
    public async Task<TriggerIrrigationResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "irrigation/trigger")] HttpRequestData req)
    {
        if (await InternalApiKeyGuard.ValidateAsync(req) is { } unauthorized)
        {
            return new TriggerIrrigationResult { HttpResponse = unauthorized, QueueMessage = null };
        }

        var body = await req.ReadAsStringAsync();
        IrrigationCommandMessage? command = null;
        try
        {
            command = string.IsNullOrWhiteSpace(body)
                ? null
                : JsonSerializer.Deserialize<IrrigationCommandMessage>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid irrigation command payload received");
        }

        if (command is null || string.IsNullOrWhiteSpace(command.DeviceId))
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await errorResponse.WriteAsJsonAsync(new { error = "deviceId is required" });
            return new TriggerIrrigationResult { HttpResponse = errorResponse, QueueMessage = null };
        }

        command.RequestedAtUtc = DateTimeOffset.UtcNow;
        _logger.LogInformation("Queuing irrigation command for device {DeviceId}", command.DeviceId);

        var message = JsonSerializer.Serialize(command);

        var response = req.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new { status = "queued", command });

        return new TriggerIrrigationResult { HttpResponse = response, QueueMessage = message };
    }
}
