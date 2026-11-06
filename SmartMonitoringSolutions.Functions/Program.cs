using Azure.Data.Tables;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

// Register a TableServiceClient used by the HTTP GET telemetry function.
// Uses the same AzureWebJobsStorage connection string as the rest of the storage bindings
// (account-key based; this subscription's restricted role cannot grant managed-identity RBAC
// access to storage, so a connection string is used instead of a managed-identity + URI).
builder.Services.AddSingleton(_ =>
{
    var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage") ?? "UseDevelopmentStorage=true";
    return new TableServiceClient(connectionString);
});

builder.Build().Run();

