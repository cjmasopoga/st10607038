using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartMonitoringSolutions.Models;
using SmartMonitoringSolutions.Services;

namespace SmartMonitoringSolutions.Controllers;

/// <summary>
/// Drone image / farm media upload. Images are uploaded directly to the "drone-images" Blob
/// Storage container, which fires the BlobTrigger Azure Function to log metadata into Table
/// Storage automatically.
/// </summary>
[Authorize(Roles = $"{nameof(UserRole.Agronomist)},{nameof(UserRole.Admin)}")]
public class MediaController : Controller
{
    private readonly BlobStorageService _blobStorageService;

    public MediaController(BlobStorageService blobStorageService)
    {
        _blobStorageService = blobStorageService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var images = await _blobStorageService.ListDroneImagesAsync();
        return View(images);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(MediaUploadViewModel model)
    {
        if (model.File is null || model.File.Length == 0)
        {
            TempData["Message"] = "Please choose a file to upload.";
            return RedirectToAction(nameof(Index));
        }

        await using var stream = model.File.OpenReadStream();
        var url = await _blobStorageService.UploadDroneImageAsync(stream, model.File.FileName, model.File.ContentType);

        TempData["Message"] = $"Uploaded '{model.File.FileName}'. A Blob-triggered function will log it to Table Storage.";
        return RedirectToAction(nameof(Index));
    }
}
