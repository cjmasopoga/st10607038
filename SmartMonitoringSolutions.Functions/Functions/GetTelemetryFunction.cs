using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SmartMonitoringSolutions.Functions.Models;
using System.Net;

namespace SmartMonitoringSolutions.Functions.Functions;

/// <summary>
/// HTTP GET trigger. Called by the MVC web app's farmer field dashboard to retrieve the
/// most recent telemetry readings for a given field from Azure Table Storage, enabling
/// near-instant dashboard rendering without the web app needing direct storage access.
/// Route: GET /api/telemetry/{fieldId}?top=20
/// </summary>
public class GetTelemetryFunction
{
    private readonly ILogger<GetTelemetryFunction> _logger;
    private readonly TableServiceClient _tableServiceClient;

    public GetTelemetryFunction(ILogger<GetTelemetryFunction> logger, TableServiceClient tableServiceClient)
    {
        _logger = logger;
        _tableServiceClient = tableServiceClient;
    }

    [Function("GetTelemetry")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "telemetry/{fieldId}")] HttpRequestData req,
        string fieldId)
    {
        if (await InternalApiKeyGuard.ValidateAsync(req) is { } unauthorized)
        {
            return unauthorized;
        }

        var top = 20;
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        if (int.TryParse(query["top"], out var parsedTop) && parsedTop > 0 && parsedTop <= 200)
        {
            top = parsedTop;
        }

        _logger.LogInformation("Retrieving latest {Top} telemetry readings for field {FieldId}", top, fieldId);

        var tableClient = _tableServiceClient.GetTableClient("TelemetryData");
        await tableClient.CreateIfNotExistsAsync();

        var results = new List<TelemetryEntity>();
        await foreach (var entity in tableClient.QueryAsync<TelemetryEntity>(e => e.PartitionKey == fieldId))
        {
            results.Add(entity);
            if (results.Count >= top * 5)
            {
                // Safety cap on scan size for very busy partitions; RowKey ordering below returns the true latest.
                break;
            }
        }

        // RowKey is an inverted timestamp (see TelemetrySimulator), so ascending RowKey order is most-recent-first.
        var latest = results.OrderBy(e => e.RowKey).Take(top);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(latest);
        return response;
    }
}
