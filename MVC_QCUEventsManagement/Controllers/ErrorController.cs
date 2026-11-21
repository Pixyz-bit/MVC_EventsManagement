using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MVC_QCUEventsManagement.Models;

namespace MVC_QCUEventsManagement.Controllers;

public sealed class ErrorController : Controller
{
    [Route("Home/Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index() => View(new ErrorViewModel
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
    });
}
