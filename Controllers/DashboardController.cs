using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartMonitoringSolutions.Models;
using SmartMonitoringSolutions.Services;

namespace SmartMonitoringSolutions.Controllers;

/// <summary>
/// Farmer field dashboard. Calls the GetTelemetry Azure Function (HTTP GET trigger) so the
/// latest soil-sensor / environmental telemetry renders near-instantly without the web app
/// talking to Table Storage directly.
/// </summary>
[Authorize]
public class DashboardController : Controller
{
    private readonly FunctionsApiClient _functionsApi;
    private readonly TelemetryTableService _telemetryTableService;

    public DashboardController(FunctionsApiClient functionsApi, TelemetryTableService telemetryTableService)
    {
        _functionsApi = functionsApi;
        _telemetryTableService = telemetryTableService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string fieldId = "field-01")
    {
        var model = new DashboardViewModel { FieldId = fieldId };
        try
        {
            model.Readings = await _functionsApi.GetLatestTelemetryAsync(fieldId);
        }
        catch (Exception ex)
        {
            model.ErrorMessage = $"Could not reach the telemetry function: {ex.Message}";
        }

        return View(model);
    }

    /// <summary>Demo-only endpoint simulating a soil sensor writing a reading straight into Table Storage.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Simulate(SimulateTelemetryViewModel model)
    {
        if (ModelState.IsValid)
        {
            await _telemetryTableService.WriteReadingAsync(model.FieldId, model.DeviceId, model.ReadingType, model.Value, model.Unit);
            TempData["Message"] = $"Simulated {model.ReadingType} reading for {model.DeviceId} on {model.FieldId}.";
        }
        return RedirectToAction(nameof(Index), new { fieldId = model.FieldId });
    }
}
