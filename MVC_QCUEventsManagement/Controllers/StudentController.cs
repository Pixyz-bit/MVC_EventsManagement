using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVC_QCUEventsManagement.ViewModels.Student;

namespace MVC_QCUEventsManagement.Controllers;

[Authorize(Roles = "Student")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StudentController : Controller
{
    [HttpGet("student")]
    public IActionResult Index() => View(new StudentLandingViewModel(User.Identity?.Name ?? ""));
}
