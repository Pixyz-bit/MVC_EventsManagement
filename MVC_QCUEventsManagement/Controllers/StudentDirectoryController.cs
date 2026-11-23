using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Services;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StudentDirectoryController(StudentService students, StudentCsvService csv, ILogger<StudentDirectoryController> logger) : Controller
{
    [HttpGet("admin/students", Name = "AdminStudentDirectory")]
    public async Task<IActionResult> Index([FromQuery] StudentDirectoryQuery query, CancellationToken ct) => View(await students.GetAsync(query, ct));
    [HttpPost("admin/students/create")]
    public async Task<IActionResult> Create([Bind(Prefix = "Input")] StudentProfileInput input, StudentDirectoryQuery query, CancellationToken ct) => await SaveAsync(null, input, query, "create", ct);
    [HttpPost("admin/students/edit")]
    public async Task<IActionResult> Edit([Bind(Prefix = "Input")] StudentProfileInput input, StudentDirectoryQuery query, CancellationToken ct) => await SaveAsync(input.StudentId, input, query, "edit", ct);
    private async Task<IActionResult> SaveAsync(string? id, StudentProfileInput input, StudentDirectoryQuery query, string operation, CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid) throw new ValidationException("Review the required fields and valid email address.");
            if (id is null) await students.CreateAsync(input, ct); else await students.UpdateAsync(id, input, "edit", ct);
            TempData["StudentMessage"] = id is null ? "Student and linked account created." : "Student profile and linked account updated.";
            return RedirectToRoute("AdminStudentDirectory", query);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is ValidationException or DbUpdateException)
        {
            if (ex is DbUpdateException) logger.LogWarning(ex, "Student profile update could not be saved");
            ModelState.AddModelError("", ex is ValidationException ? ex.Message : "This profile could not be saved. Reload and try again.");
            input.Password = null; var model = await students.GetAsync(query, ct); model.Input = input; model.Reopen = operation;
            return View("Index", model);
        }
    }
    [HttpPost("admin/students/access")]
    public async Task<IActionResult> Access(string id, string token, bool active, bool confirmed, StudentDirectoryQuery query, CancellationToken ct) => await AccountChangeAsync(id, token, active, null, confirmed, "access", query, ct);
    [HttpPost("admin/students/password")]
    public async Task<IActionResult> Password(string id, string token, string? password, bool confirmed, StudentDirectoryQuery query, CancellationToken ct) => await AccountChangeAsync(id, token, false, password, confirmed, "password", query, ct);
    private async Task<IActionResult> AccountChangeAsync(string id, string token, bool active, string? password, bool confirmed, string operation, StudentDirectoryQuery query, CancellationToken ct)
    {
        try
        {
            if (!confirmed) throw new ValidationException("Confirm the requested account change.");
            await students.UpdateAsync(id, new() { Token = token, Active = active, Password = password }, operation, ct);
            TempData["StudentMessage"] = operation == "password" ? "Student password reset." : "Student account access updated. Profile and registration history retained.";
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is ValidationException or DbUpdateException)
        {
            if (ex is DbUpdateException) logger.LogWarning(ex, "Student account change could not be saved");
            TempData["StudentError"] = ex is ValidationException ? ex.Message : "The account could not be updated. Reload and try again.";
        }
        return RedirectToRoute("AdminStudentDirectory", query);
    }
    [HttpGet("admin/students/template")]
    public IActionResult Template() => File(System.Text.Encoding.UTF8.GetBytes(StudentCsvService.Header + "\r\n"), "text/csv; charset=utf-8", "student-import-template.csv");
    [HttpGet("admin/students/export")]
    public async Task<IActionResult> Export([FromQuery] StudentDirectoryQuery query, CancellationToken ct)
    {
        try { return File(await students.ExportAsync(query, ct), "text/csv; charset=utf-8", "student-directory.csv"); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }
    [HttpPost("admin/students/import"), RequestSizeLimit(2_300_000)]
    public async Task<IActionResult> Import(IFormFile? roster, string? password, StudentDirectoryQuery query, CancellationToken ct)
    {
        StudentImportReport? report = null;
        try
        {
            if (roster is null || roster.Length is 0 or > 2_097_152 || !string.Equals(Path.GetExtension(roster.FileName), ".csv", StringComparison.OrdinalIgnoreCase)) throw new ValidationException("Select a CSV file up to 2 MB.");
            await using var stream = roster.OpenReadStream(); report = await csv.ImportAsync(stream, password ?? "", ct);
        }
        catch (Exception ex) when (ex is ValidationException or System.Text.DecoderFallbackException)
        { ModelState.AddModelError("", ex is ValidationException ? ex.Message : "The file must contain valid UTF-8 text."); }
        var model = await students.GetAsync(query, ct); model.ImportReport = report; model.Reopen = "import";
        return View("Index", model);
    }
}
