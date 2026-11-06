using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SmartMonitoringSolutions.Functions.Models;
using System.Text.Json;

namespace SmartMonitoringSolutions.Functions.Functions;

/// <summary>
/// Queue-triggered function. Listens to the "irrigation-commands" Azure Storage Queue,
/// processes each irrigation command, and updates the device's operational status in the
/// master "DeviceRegistry" table in Azure SQL Database.
/// </summary>
public class ProcessIrrigationQueueTrigger
{
    private readonly ILogger<ProcessIrrigationQueueTrigger> _logger;

    public ProcessIrrigationQueueTrigger(ILogger<ProcessIrrigationQueueTrigger> logger)
    {
        _logger = logger;
    }

    [Function("ProcessIrrigationQueueTrigger")]
    public async Task Run([QueueTrigger("irrigation-commands", Connection = "AzureWebJobsStorage")] string queueMessage)
    {
        IrrigationCommandMessage? command;
        try
        {
            command = JsonSerializer.Deserialize<IrrigationCommandMessage>(queueMessage, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse irrigation command message: {Message}", queueMessage);
            return;
        }

        if (command is null || string.IsNullOrWhiteSpace(command.DeviceId))
        {
            _logger.LogWarning("Ignoring irrigation command with missing deviceId: {Message}", queueMessage);
            return;
        }

        var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogError("SqlConnectionString app setting is not configured. Cannot update DeviceRegistry.");
            return;
        }

        _logger.LogInformation("Processing irrigation command for device {DeviceId}", command.DeviceId);

        const string sql = @"
MERGE DeviceRegistry AS target
USING (SELECT @DeviceId AS DeviceId) AS source
ON target.DeviceId = source.DeviceId
WHEN MATCHED THEN
    UPDATE SET Status = 'Irrigating', LastCommand = @Reason, LastUpdatedUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (DeviceId, DeviceType, Status, LastCommand, LastUpdatedUtc)
    VALUES (@DeviceId, 'IrrigationController', 'Irrigating', @Reason, SYSUTCDATETIME());";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var sqlCommand = new SqlCommand(sql, connection);
        sqlCommand.Parameters.AddWithValue("@DeviceId", command.DeviceId);
        sqlCommand.Parameters.AddWithValue("@Reason", string.IsNullOrWhiteSpace(command.Reason) ? "Irrigation triggered" : command.Reason);

        var rows = await sqlCommand.ExecuteNonQueryAsync();
        _logger.LogInformation("DeviceRegistry updated for {DeviceId}. Rows affected: {Rows}", command.DeviceId, rows);
    }
}
