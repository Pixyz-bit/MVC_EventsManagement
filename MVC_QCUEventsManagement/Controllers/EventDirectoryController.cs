using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVC_QCUEventsManagement.Services;

namespace MVC_QCUEventsManagement.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EventDirectoryController(EventService events) : Controller
{
    [HttpGet("admin", Name = "AdminPortal")]
    [HttpGet("admin/events", Name = "AdminEvents")]
    public async Task<IActionResult> Index(string? q, string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        return View(await events.GetUpcomingAsync(q, status, page, cancellationToken));
    }
}
