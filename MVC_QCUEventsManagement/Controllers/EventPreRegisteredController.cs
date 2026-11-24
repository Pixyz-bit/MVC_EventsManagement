using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Services;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EventPreRegisteredController(RegistrationService registrations, ILogger<EventPreRegisteredController> logger) : Controller
{
    [HttpGet("admin/events/{id:int}/preregistered", Name = "AdminEventPreRegistered")]
    public async Task<IActionResult> Index(int id, [FromQuery] EventRosterQuery query, CancellationToken cancellationToken)
    {
        var model = await registrations.GetRosterAsync(id, query, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost("admin/events/{id:int}/preregistered/{registrationId:int}/void")]
    public async Task<IActionResult> Void(int id, int registrationId, VoidPassInput input, EventRosterQuery query, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid) throw new ValidationException("Reload the roster before cancelling a pass.");
            await registrations.VoidPassAsync(id, registrationId, input, cancellationToken);
            TempData["RosterMessage"] = "Pass cancelled. Its history has been retained and the seat count updated.";
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ValidationException ex) { TempData["RosterError"] = ex.Message; }
        catch (DbUpdateConcurrencyException) { TempData["RosterError"] = "This roster changed. Review the latest attendee details before trying again."; }
        catch (Exception ex) when (ex is DbUpdateException or System.Data.Common.DbException
            || ex is InvalidOperationException && ex.GetBaseException() is System.Data.Common.DbException)
        { logger.LogError(ex, "Could not cancel pass {RegistrationId} for event {EventId}", registrationId, id); TempData["RosterError"] = "Could not cancel the pass. Please try again."; }
        if (TempData.ContainsKey("RosterError")) TempData["RosterAttendee"] = registrationId;
        return RedirectToRoute("AdminEventPreRegistered", new { id, query.Q, query.College, query.Program, query.Year, query.Cohort, query.Page, from = query.From == "history" ? "history" : null });
    }
}
