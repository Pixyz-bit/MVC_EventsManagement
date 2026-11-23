using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MVC_QCUEventsManagement.Services;

namespace MVC_QCUEventsManagement.Controllers;

[Authorize(Roles = "Student")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StudentController(StudentDashboardService dashboard, EventBannerStore banners) : Controller
{
    private int StudentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet("student", Name = "StudentDashboard")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await dashboard.GetAsync(StudentUserId, User.Identity?.Name ?? "", ct));

    [HttpGet("student/events/{id:int}/banner", Name = "StudentEventBanner")]
    public async Task<IActionResult> Banner(int id, CancellationToken ct)
    {
        var name = await dashboard.GetBannerAsync(StudentUserId, id, ct);
        if (name is null) return NotFound();
        try
        {
            var path = banners.Resolve(name);
            return System.IO.File.Exists(path) ? PhysicalFile(path, "image/png") : NotFound();
        }
        catch (InvalidDataException) { return NotFound(); }
    }
}
