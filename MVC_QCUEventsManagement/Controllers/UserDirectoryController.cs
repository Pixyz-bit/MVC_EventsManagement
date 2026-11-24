using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Services;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UserDirectoryController(AccountManagementService accounts, StudentCsvService csv,
    ILogger<UserDirectoryController> logger) : Controller
{
    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private Task<UserDirectoryViewModel> ListAsync(UserDirectoryQuery query, CancellationToken ct) =>
        accounts.ListAsync(query.Q, query.Role, query.Status, query.Page, ActorId, ct, query.College, query.Program, query.Campus);

    [HttpGet("admin/users", Name = "AdminUserDirectory")]
    [HttpGet("admin/accounts", Name = "AdminAccounts")]
    [HttpGet("admin/students", Name = "AdminStudentDirectory")]
    public async Task<IActionResult> Index([FromQuery] UserDirectoryQuery query, int? id, string? open, CancellationToken ct)
    {
        if (Request.Path == "/admin/students" && query.Role is null) query.Role = "Student";
        try
        {
            var editor = id is { } selected ? await accounts.GetAsync(selected, ct) : null;
            return View((await ListAsync(query, ct)) with { Editor = editor, Open = editor is null && open is "create" or "import" ? open : null });
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("admin/users/create")]
    public async Task<IActionResult> Create([Bind(Prefix = "NewAccount")] CreateAccountInput input, UserDirectoryQuery query, CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid) throw new ValidationException("Review the required account fields.");
            await accounts.CreateAsync(input, ActorId, ct);
            TempData["AccountMessage"] = input.Role == "Student" ? "Student profile and account created." : "Administrator account created.";
            return RedirectToRoute("AdminUserDirectory", query);
        }
        catch (Exception ex) when (Handled(ex))
        {
            if (ex is not ValidationException) logger.LogWarning(ex, "Account creation could not be saved");
            input.Password = null;
            ModelState.Remove("NewAccount.Password");
            ModelState.AddModelError("", ex is ValidationException ? ex.Message : "The account could not be created. Review the directory before trying again.");
            return View("Index", (await ListAsync(query, ct)) with { NewAccount = input, Open = "create" });
        }
    }

    [HttpPost("admin/users/save")]
    [HttpPost("admin/accounts/save")]
    public async Task<IActionResult> Save([Bind(Prefix = "Editor")] ManagedAccountInput input, UserDirectoryQuery query, CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid) throw new ValidationException("Review the account and student fields.");
            await accounts.SaveAsync(input, ActorId, ct, editBirthDate: Request.Path == "/admin/users/save");
            TempData["AccountMessage"] = "Profile and account changes saved.";
            return RedirectToRoute("AdminUserDirectory", query);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (Handled(ex))
        {
            if (ex is not ValidationException) logger.LogWarning(ex, "Account {AccountId} could not be saved", input.Id);
            ModelState.Remove("Editor.Password");
            input.Password = null;
            ModelState.AddModelError("", ex is ValidationException ? ex.Message : "The account could not be saved. Reopen it and try again.");
            try
            {
                var existing = await accounts.GetAsync(input.Id, ct);
                input.HasStudentProfile = existing.HasStudentProfile;
                if (existing.HasStudentProfile)
                {
                    input.StudentId = existing.StudentId;
                    ModelState.Remove("Editor.StudentId");
                }
            }
            catch (KeyNotFoundException) { return NotFound(); }
            return View("Index", (await ListAsync(query, ct)) with { Editor = input });
        }
    }

    [HttpGet("admin/users/export")]
    [HttpGet("admin/accounts/export")]
    [HttpGet("admin/students/export")]
    public async Task<IActionResult> Export([FromQuery] UserDirectoryQuery query, CancellationToken ct)
    {
        if (Request.Path == "/admin/students/export" && query.Role is null) query.Role = "Student";
        try { return File(await accounts.ExportAsync(query.Q, query.Role, query.Status, ct, query.College, query.Program, query.Campus), "text/csv; charset=utf-8", "user-directory.csv"); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("admin/users/template")]
    [HttpGet("admin/students/template")]
    public IActionResult Template() => File(System.Text.Encoding.UTF8.GetBytes(StudentCsvService.Header + "\r\n"), "text/csv; charset=utf-8", "student-import-template.csv");

    [HttpPost("admin/users/import"), RequestSizeLimit(2_300_000)]
    [HttpPost("admin/students/import")]
    public async Task<IActionResult> Import(IFormFile? roster, string? password, UserDirectoryQuery query, CancellationToken ct)
    {
        StudentImportReport? report = null;
        try
        {
            if (roster is null || roster.Length is 0 or > 2_097_152 || !string.Equals(Path.GetExtension(roster.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("Select a CSV file up to 2 MB.");
            await using var stream = roster.OpenReadStream();
            report = await csv.ImportAsync(stream, password ?? "", ct);
        }
        catch (Exception ex) when (Handled(ex) || ex is System.Text.DecoderFallbackException)
        {
            if (ex is not (ValidationException or System.Text.DecoderFallbackException)) logger.LogWarning(ex, "Student import could not be completed");
            ModelState.AddModelError("", ex is ValidationException ? ex.Message : ex is System.Text.DecoderFallbackException
                ? "The file must contain valid UTF-8 text." : "Import could not be completed. Review the directory before retrying.");
        }
        return View("Index", (await ListAsync(query, ct)) with { ImportReport = report, Open = "import" });
    }

    private static bool Handled(Exception ex) => ex is ValidationException or DbUpdateException or SqlException
        || ex is InvalidOperationException && ex.GetBaseException() is SqlException;
}
