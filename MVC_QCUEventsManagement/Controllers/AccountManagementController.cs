using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Services;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountManagementController(AccountManagementService accounts, ILogger<AccountManagementController> logger) : Controller
{
    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet("admin/accounts", Name = "AdminAccounts")]
    public async Task<IActionResult> Index(string? q, string? role, string? status, int page = 1, int? id = null, CancellationToken ct = default)
    {
        try
        {
            var editor = id is { } selected ? await accounts.GetAsync(selected, ct) : null;
            var model = await accounts.ListAsync(q, role, status, page, ActorId, ct);
            return View(model with { Editor = editor });
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("admin/accounts/save")]
    public async Task<IActionResult> Save([Bind(Prefix = "Editor")] ManagedAccountInput input,
        string? q, string? role, string? status, int page, CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid) throw new ValidationException("Review the account and student fields.");
            await accounts.SaveAsync(input, ActorId, ct);
            TempData["AccountMessage"] = "Account changes saved.";
            return RedirectToRoute("AdminAccounts", new { q, role, status, page });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is ValidationException or DbUpdateException or Microsoft.Data.SqlClient.SqlException)
        {
            if (ex is not ValidationException) logger.LogWarning(ex, "Account {AccountId} could not be saved", input.Id);
            ModelState.Remove("Editor.Password");
            ModelState.AddModelError("", ex is ValidationException ? ex.Message : "The account could not be saved. Reopen it and try again.");
            input.Password = null;
            try
            {
                var existing = await accounts.GetAsync(input.Id, ct);
                input.BirthDate = existing.BirthDate;
                input.HasStudentProfile = existing.HasStudentProfile;
                if (existing.HasStudentProfile)
                {
                    input.StudentId = existing.StudentId;
                    ModelState.Remove("Editor.StudentId");
                }
            }
            catch (KeyNotFoundException) { return NotFound(); }
            var model = await accounts.ListAsync(q, role, status, page, ActorId, ct);
            return View("Index", model with { Editor = input });
        }
    }

    [HttpGet("admin/accounts/export")]
    public async Task<IActionResult> Export(string? q, string? role, string? status, CancellationToken ct)
    {
        try { return File(await accounts.ExportAsync(q, role, status, ct), "text/csv; charset=utf-8", "accounts.csv"); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }
}
