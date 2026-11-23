using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MVC_QCUEventsManagement.Services;
using MVC_QCUEventsManagement.ViewModels.Student;
using Microsoft.AspNetCore.RateLimiting;

namespace MVC_QCUEventsManagement.Controllers;

[Authorize(Roles = "Student")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StudentController(StudentDashboardService dashboard, EventBannerStore banners, StudentPortalService portal) : Controller
{
    private int StudentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet("student", Name = "StudentDashboard")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await dashboard.GetAsync(StudentUserId, User.Identity?.Name ?? "", ct));

    [HttpGet("student/profile", Name = "StudentProfile")]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        var profile = await portal.ProfileAsync(StudentUserId, ct);
        return profile is null ? NotFound() : View(profile);
    }

    [HttpPost("student/profile/password")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ChangePassword(StudentPasswordInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            TempData["StudentError"] = "Enter your current password and matching new passwords of 15–128 characters.";
        else if (!await portal.ChangePasswordAsync(StudentUserId, input, ct))
            TempData["StudentError"] = "The current password is incorrect or the account changed.";
        else
            TempData["StudentMessage"] = "Your password was updated.";
        return RedirectToRoute("StudentProfile", null, "security");
    }

    [HttpGet("student/events/{id:int}/register", Name = "StudentEventRegistration")]
    public async Task<IActionResult> Registration(int id, CancellationToken ct)
    {
        if (await portal.ActivePassIdAsync(StudentUserId, id, ct) is int passId)
            return RedirectToRoute("StudentPass", new { id = passId });
        var booking = await portal.BookingAsync(StudentUserId, id, ct);
        return View(booking);
    }

    [HttpPost("student/events/{id:int}/register")]
    public async Task<IActionResult> Book(int id, StudentBookingInput input, CancellationToken ct)
    {
        if (await portal.ActivePassIdAsync(StudentUserId, id, ct) is int existing)
            return RedirectToRoute("StudentPass", new { id = existing });
        if (!input.Terms) ModelState.AddModelError(nameof(input.Terms), "Confirm that you agree to the event guidelines.");
        if (ModelState.IsValid)
        {
            try
            {
                var passId = await portal.BookAsync(StudentUserId, id, input, ct);
                TempData["BookedPass"] = passId;
                return RedirectToRoute("StudentPass", new { id = passId });
            }
            catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
        }
        var booking = await portal.BookingAsync(StudentUserId, id, ct);
        ViewData["BookingInput"] = input;
        return View("Registration", booking);
    }

    [HttpGet("student/passes/{id:int}", Name = "StudentPass")]
    public async Task<IActionResult> Pass(int id, CancellationToken ct)
    {
        var pass = await portal.PassAsync(StudentUserId, id, TempData.Peek("BookedPass") is int booked && booked == id, ct);
        if (pass is null) return NotFound();
        if (pass.JustBooked) TempData.Remove("BookedPass");
        return View(pass);
    }

    [HttpPost("student/passes/{id:int}/cancel")]
    public async Task<IActionResult> CancelPass(int id, CancellationToken ct)
    {
        try
        {
            await portal.CancelAsync(StudentUserId, id, ct);
            TempData["StudentMessage"] = "Your registration was cancelled.";
        }
        catch (InvalidOperationException ex) { TempData["StudentError"] = ex.Message; }
        return RedirectToRoute("StudentDashboard", new { view = "registered" }, "events-section");
    }

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
