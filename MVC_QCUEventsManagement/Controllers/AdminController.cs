using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVC_QCUEventsManagement.Services;
using MVC_QCUEventsManagement.ViewModels.Admin;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MVC_QCUEventsManagement.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminController(EventService events, EventBannerStore banners, ILogger<AdminController> logger) : Controller
{
    [HttpGet("admin/events/create", Name = "AdminCreateEvent")]
    public IActionResult Create() => View(new CreateEventInput());

    [HttpPost("admin/events/create")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<IActionResult> Create(CreateEventInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(input);
        string? bannerName = null;
        var saved = false;
        try
        {
            if (input.Banner is not null) bannerName = await banners.SaveAsync(input.Banner, cancellationToken);
            await events.CreateAsync(input, int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), bannerName, cancellationToken);
            saved = true;
            TempData["EventCreated"] = "Event published successfully.";
            return RedirectToRoute("AdminEvents");
        }
        catch (InvalidDataException ex) { ModelState.AddModelError(nameof(input.Banner), ex.Message); }
        catch (ValidationException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) when (ex is DbUpdateException or IOException)
        {
            logger.LogError(ex, "Event publication failed");
            ModelState.AddModelError(string.Empty, "We couldn’t publish the event. Please try again.");
        }
        finally
        {
            if (!saved && bannerName is not null)
            {
                try { banners.Delete(bannerName); }
                catch (IOException ex) { logger.LogError(ex, "Could not remove unused event banner {Banner}", bannerName); }
            }
        }
        if (input.Banner is not null)
            ModelState.AddModelError(nameof(input.Banner), "Select the banner again before retrying; other details have been kept.");
        return View(input);
    }

    [HttpGet("admin/events/{id:int}/banner")]
    public async Task<IActionResult> Banner(int id, CancellationToken cancellationToken)
    {
        var name = await events.GetBannerAsync(id, cancellationToken);
        if (name is null) return NotFound();
        var path = banners.Resolve(name);
        if (!System.IO.File.Exists(path)) return NotFound();
        Response.Headers.XContentTypeOptions = "nosniff";
        return PhysicalFile(path, "image/png");
    }

    [HttpGet("admin", Name = "AdminPortal")]
    [HttpGet("admin/events", Name = "AdminEvents")]
    public async Task<IActionResult> Index(string? q, string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        return View("Events", await events.GetUpcomingAsync(q, status, page, cancellationToken));
    }
}
