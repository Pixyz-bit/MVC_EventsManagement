using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVC_QCUEventsManagement.Services;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Controllers;

[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DevelopmentPreviewController(IWebHostEnvironment environment, IConfiguration configuration, EventService events) : Controller
{
    private bool Available => environment.IsDevelopment()
        && configuration.GetValue<bool>("DevelopmentPreview:Enabled")
        && (HttpContext.Connection.RemoteIpAddress is null || IPAddress.IsLoopback(HttpContext.Connection.RemoteIpAddress));

    private void Prepare()
    {
        ViewData["PreviewMode"] = true;
        // A rendering identity only: no authentication cookie is issued or permissions granted.
        HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Design preview")]));
    }

    [HttpGet("preview/admin/events", Name = "PreviewAdminEvents")]
    public async Task<IActionResult> Events(string? q, string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        if (!Available) return NotFound();
        Prepare();
        return View("~/Views/EventDirectory/Index.cshtml", await events.GetUpcomingAsync(q, status, page, cancellationToken));
    }

    [HttpGet("preview/admin/events/create", Name = "PreviewCreateEvent")]
    public IActionResult Create()
    {
        if (!Available) return NotFound();
        Prepare();
        return View("~/Views/CreateEvent/Create.cshtml", new CreateEventInput());
    }
}
