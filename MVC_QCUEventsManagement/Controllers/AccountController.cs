using Microsoft.AspNetCore.Mvc;

namespace MVC_QCUEventsManagement.Controllers;

public sealed class AccountController : Controller
{
    [HttpGet("login")]
    public IActionResult Login() => View();
}
