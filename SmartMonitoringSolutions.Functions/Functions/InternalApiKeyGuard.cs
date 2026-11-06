using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace SmartMonitoringSolutions.Functions.Functions;

/// <summary>
/// Lightweight shared-secret check for the HTTP-triggered functions. The MVC web app and this
/// Function App are both given the same "InternalApiKey" app setting at deploy time (see
/// infra/modules/resources.bicep); the web app sends it as the "x-internal-api-key" header on
/// every call so the public HTTP endpoints cannot be invoked by unrelated callers.
/// </summary>
internal static class InternalApiKeyGuard
{
    public static async Task<HttpResponseData?> ValidateAsync(HttpRequestData req)
    {
        var expectedKey = Environment.GetEnvironmentVariable("InternalApiKey");
        if (string.IsNullOrEmpty(expectedKey))
        {
            // No key configured (e.g. local development) — allow the call through.
            return null;
        }

        if (!req.Headers.TryGetValues("x-internal-api-key", out var values) ||
            !values.Any(v => string.Equals(v, expectedKey, StringComparison.Ordinal)))
        {
            var response = req.CreateResponse(HttpStatusCode.Unauthorized);
            await response.WriteStringAsync("Missing or invalid x-internal-api-key header.");
            return response;
        }

        return null;
    }
}
