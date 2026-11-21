using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVC_QCUEventsManagement.ViewModels.Account;

namespace MVC_QCUEventsManagement.Controllers;

[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccessDeniedController : Controller
{
    [HttpGet("access-denied")]
    public IActionResult Index()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View(new AccessDeniedViewModel(User.Identity?.IsAuthenticated == true));
    }
}
