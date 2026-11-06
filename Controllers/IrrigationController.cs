using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartMonitoringSolutions.Models;
using SmartMonitoringSolutions.Services;

namespace SmartMonitoringSolutions.Controllers;

/// <summary>
/// Handles "Trigger Irrigation" requests. Calls the TriggerIrrigation Azure Function
/// (HTTP POST trigger), which places a command message onto the Azure Storage Queue for
/// asynchronous processing by the Queue-triggered function.
/// </summary>
[Authorize(Roles = $"{nameof(UserRole.Farmer)},{nameof(UserRole.Agronomist)},{nameof(UserRole.Admin)}")]
public class IrrigationController : Controller
{
    private readonly FunctionsApiClient _functionsApi;

    public IrrigationController(FunctionsApiClient functionsApi)
    {
        _functionsApi = functionsApi;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Trigger(TriggerIrrigationViewModel model)
    {
        if (ModelState.IsValid)
        {
            var success = await _functionsApi.TriggerIrrigationAsync(model);
            TempData["Message"] = success
                ? $"Irrigation command queued for device '{model.DeviceId}'."
                : "Failed to queue the irrigation command. Please try again.";
        }

        return RedirectToAction("Index", "Dashboard", new { fieldId = model.FieldId });
    }
}
