using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVC_QCUEventsManagement.Services;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EventAttendanceController(AttendanceService attendance) : Controller
{
    [HttpGet("admin/events/{id:int}/attendance", Name = "AdminEventAttendance")]
    public async Task<IActionResult> Index(int id, [FromQuery] EventAttendanceQuery query, CancellationToken cancellationToken)
    {
        var model = await attendance.GetAttendanceAsync(id, query, cancellationToken);
        return model is null ? NotFound() : View(model);
    }
}
