using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MVC_QCUEventsManagement.Controllers;

[Authorize(Roles = "Student")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StudentController : Controller
{
    [HttpGet("student")]
    public IActionResult Index() => View("PortalLanding", "Student portal");
}
