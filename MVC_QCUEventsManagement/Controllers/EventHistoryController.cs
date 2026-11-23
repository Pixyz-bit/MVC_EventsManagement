using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVC_QCUEventsManagement.Services;

namespace MVC_QCUEventsManagement.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EventHistoryController(EventService events) : Controller
{
    [HttpGet("admin/events/history", Name = "AdminEventHistory")]
    public async Task<IActionResult> Index(string? q, int? year, string? outcome, int page = 1, CancellationToken cancellationToken = default)
    {
        return View(await events.GetHistoryAsync(q, year, outcome, page, cancellationToken));
    }
}
