using Microsoft.AspNetCore.Mvc;

namespace MVC_QCUEventsManagement.Controllers;

public sealed class PrivacyController : Controller
{
    [HttpGet("Home/Privacy")]
    public IActionResult Index() => View();
}
