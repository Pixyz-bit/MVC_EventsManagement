using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVC_QCUEventsManagement.Services;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EventAnalyticsController(AnalyticsService analytics) : Controller
{
    [HttpGet("admin/events/{id:int}/analytics", Name = "AdminEventAnalytics")]
    public async Task<IActionResult> Index(int id, [FromQuery] EventAnalyticsQuery query, CancellationToken cancellationToken)
    {
        var model = await analytics.GetAsync(id, query, cancellationToken);
        return model is null ? NotFound() : View(model);
    }
}
