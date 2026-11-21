using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Data;

namespace MVC_QCUEventsManagement.Services;

public sealed class AccountCookieEvents(ApplicationDbContext db) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var account = await db.Users.AsNoTracking()
                .Where(user => user.UserId == userId && user.IsActive)
                .Select(user => new { user.Email, user.Role })
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

            if (account is { Role: "Admin" or "Student" }
                && context.Principal!.FindFirstValue(ClaimTypes.Role) == account.Role
                && context.Principal.Identity?.Name == account.Email)
            {
                return;
            }
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }
}
