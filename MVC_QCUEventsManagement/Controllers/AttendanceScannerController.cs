using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Services;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AttendanceScannerController(AttendanceService attendance, ILogger<AttendanceScannerController> logger) : Controller
{
    [HttpGet("admin/events/{id:int}/scanner", Name = "AdminAttendanceScanner")]
    public async Task<IActionResult> Index(int id, string? from, CancellationToken cancellationToken)
    {
        var model = await attendance.GetScannerAsync(id, from == "history", cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost("admin/events/{id:int}/scanner/lookup")]
    public async Task<IActionResult> Lookup(int id, string? query, CancellationToken cancellationToken)
    {
        try { return Json(await attendance.LookupAsync(id, query, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(new { message = "The selected event no longer exists." }); }
        catch (Exception ex) when (ex is DbUpdateException or System.Data.Common.DbException
            || ex is InvalidOperationException && ex.GetBaseException() is System.Data.Common.DbException)
        { logger.LogError(ex, "Could not look up an attendee for event {EventId}", id); return StatusCode(503, new { message = "Lookup is unavailable. Please try again." }); }
    }

    [HttpPost("admin/events/{id:int}/scanner/confirm")]
    public async Task<IActionResult> Confirm(int id, CheckInInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new { message = "Look up the pass again before confirming check-in." });
        try { return Json(await attendance.ConfirmAsync(id, input, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(new { message = "The selected event no longer exists." }); }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex) when (ex is DbUpdateException or System.Data.Common.DbException
            || ex is InvalidOperationException && ex.GetBaseException() is System.Data.Common.DbException)
        { logger.LogError(ex, "Could not check in attendee for event {EventId}", id); return StatusCode(503, new { message = "Check-in could not be confirmed. Look up the pass again before retrying." }); }
    }
}
