using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Services;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EventDetailsController(EventService events, EventBannerStore banners, ILogger<EventDetailsController> logger) : Controller
{
    [HttpGet("admin/events/{id:int}", Name = "AdminEventDetails")]
    public async Task<IActionResult> Index(int id, bool edit = false, CancellationToken cancellationToken = default)
    {
        var model = await events.GetDetailsAsync(id, edit, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost("admin/events/{id:int}/edit")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Edit(int id, [Bind(Prefix = "Input")] CreateEventInput input, string? version, bool removeBanner, CancellationToken cancellationToken)
    {
        string? uploaded = null;
        var saved = false;
        try
        {
            if (ModelState.IsValid)
            {
                var token = Version(version);
                if (input.Banner is not null) uploaded = await banners.SaveAsync(input.Banner, cancellationToken);
                var obsolete = await events.EditAsync(id, input, token, uploaded, removeBanner, cancellationToken);
                saved = true;
                if (obsolete is not null) Clean(obsolete);
                TempData["EventMessage"] = "Event changes saved.";
                return RedirectToRoute("AdminEventDetails", new { id });
            }
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is ValidationException or InvalidDataException) { ModelState.AddModelError("", ex.Message); }
        catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "This event changed while you were editing. Reload it and review the latest details before trying again."); }
        catch (Exception ex) when (ex is DbUpdateException or IOException)
        { logger.LogError(ex, "Could not update event {EventId}", id); ModelState.AddModelError("", "Could not save the event. Please try again."); }
        finally { if (!saved && uploaded is not null) Clean(uploaded); }
        var current = await events.GetDetailsAsync(id, false, cancellationToken);
        if (current is null) return NotFound();
        ViewData["EditVersion"] = version;
        if (input.Banner is not null) ModelState.AddModelError("Input.Banner", "Select the banner again before retrying.");
        return View("Index", new EventDetailsViewModel { Event = current.Event, Input = input, CanManage = current.CanManage, Editing = current.CanManage });
    }

    [HttpPost("admin/events/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, string? reason, string? version, bool confirmed, CancellationToken cancellationToken)
    {
        try
        {
            if (!confirmed) throw new ValidationException("Confirm that you want to cancel this event.");
            await events.CancelAsync(id, reason, Version(version), cancellationToken);
            TempData["EventMessage"] = "Event cancelled. Its registration history has been retained.";
            return RedirectToRoute("AdminEventDetails", new { id });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ValidationException ex) { ModelState.AddModelError("", ex.Message); }
        catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "This event changed. Reload it before cancelling."); }
        catch (DbUpdateException ex) { logger.LogError(ex, "Could not cancel event {EventId}", id); ModelState.AddModelError("", "Could not cancel the event. Please try again."); }
        var current = await events.GetDetailsAsync(id, false, cancellationToken);
        return current is null ? NotFound() : View("Index", current);
    }

    private static byte[] Version(string? value)
    {
        try { var bytes = Convert.FromBase64String(value ?? ""); if (bytes.Length == 8) return bytes; }
        catch (FormatException) { }
        throw new ValidationException("Reload the event before submitting changes.");
    }
    private void Clean(string name)
    {
        try { banners.Delete(name); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        { logger.LogWarning(ex, "Unused banner could not be removed: {Banner}", name); }
    }
}
