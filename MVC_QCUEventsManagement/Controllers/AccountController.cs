using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MVC_QCUEventsManagement.Services;
using MVC_QCUEventsManagement.ViewModels.Account;

namespace MVC_QCUEventsManagement.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(AccountService accounts) : Controller
{
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return Portal(User.IsInRole("Admin") ? "Admin" : "Student");
        return View(new LoginInput { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginInput input, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true) return Portal(User.IsInRole("Admin") ? "Admin" : "Student");
        if (!ModelState.IsValid) return View(input);

        var account = await accounts.AuthenticateAsync(input.Identifier, input.Password, cancellationToken);
        if (account is null)
        {
            ModelState.AddModelError(string.Empty, "The student ID, email, or password is incorrect.");
            return View(input);
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, account.UserId.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, account.Email),
            new Claim(ClaimTypes.Role, account.Role)
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return Url.IsLocalUrl(input.ReturnUrl) ? LocalRedirect(input.ReturnUrl) : Portal(account.Role);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet("access-denied")]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private IActionResult Portal(string role) => role == "Admin"
        ? RedirectToAction(nameof(AdminController.Index), "Admin")
        : RedirectToAction(nameof(StudentController.Index), "Student");
}
