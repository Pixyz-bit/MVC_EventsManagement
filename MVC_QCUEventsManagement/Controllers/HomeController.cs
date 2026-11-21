using Microsoft.AspNetCore.Mvc;

namespace MVC_QCUEventsManagement.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() => RedirectToAction(nameof(LoginController.Login), "Login");
}
