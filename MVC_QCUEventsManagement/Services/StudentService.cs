using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Data;
using MVC_QCUEventsManagement.Models;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Services;

public sealed class StudentService(ApplicationDbContext db, IPasswordHasher<User> hasher, IDataProtectionProvider protection, EventClock clock)
{
    private readonly IDataProtector _protector = protection.CreateProtector("Qcu.StudentDirectory.v1");
    public static string[] Genders { get; } = ["Male", "Female", "Non-Binary", "Prefer not to say", "Not Specified"];
    private static byte[] Snapshot(Student s) => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new object?[] { s.StudentId, s.UserId, s.FirstName, s.MiddleName, s.LastName, s.BirthDate, s.Gender, s.CampusBranch, s.Department, s.Program, s.User.Email, s.User.IsActive, s.User.Role, s.User.PasswordHash }));
    private StudentProfileData Profile(Student s) => new(s.StudentId, s.FirstName, s.MiddleName, s.LastName, s.User.Email, s.BirthDate,
        s.Gender, s.CampusBranch, s.Department, s.Program, s.User.IsActive, _protector.Protect(Convert.ToBase64String(Snapshot(s))));
    private static string? Limit(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim()[..Math.Min(150, s.Trim().Length)];
    private IQueryable<Student> Filter(StudentDirectoryQuery input)
    {
        var rows = db.Students.AsNoTracking().Where(s => s.User.Role == "Student");
        if (Limit(input.Q) is { } q) rows = rows.Where(s => EF.Functions.Collate(s.StudentId, "Latin1_General_100_CI_AS").Contains(q)
            || EF.Functions.Collate(s.FirstName + " " + (s.MiddleName == null || s.MiddleName == "" ? "" : s.MiddleName + " ") + s.LastName, "Latin1_General_100_CI_AS").Contains(q));
        if (Limit(input.College) is { } college) rows = rows.Where(s => s.Department == college);
        if (Limit(input.Program) is { } program) rows = rows.Where(s => s.Program == program);
        if (Limit(input.Campus) is { } campus) rows = rows.Where(s => s.CampusBranch == campus);
        if (input.Status is "Active" or "Inactive") rows = rows.Where(s => s.User.IsActive == (input.Status == "Active"));
        return rows;
    }
    public async Task<StudentDirectoryViewModel> GetAsync(StudentDirectoryQuery input, CancellationToken ct)
    {
        var q = new StudentDirectoryQuery { Q = Limit(input.Q), College = Limit(input.College), Program = Limit(input.Program), Campus = Limit(input.Campus), Status = input.Status is "Active" or "Inactive" ? input.Status : null };
        var matching = Filter(q); var total = await matching.CountAsync(ct); var pages = Math.Max(1, (int)Math.Ceiling(total / 25d)); q.Page = Math.Clamp(input.Page, 1, pages);
        var rows = await matching.Include(s => s.User).OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ThenBy(s => s.StudentId).Skip((q.Page - 1) * 25).Take(25).ToListAsync(ct);
        var all = db.Students.AsNoTracking().Where(s => s.User.Role == "Student");
        var colleges = await all.Select(s => s.Department).Distinct().Order().ToListAsync(ct);
        var programs = await all.Where(s => q.College == null || s.Department == q.College).Select(s => s.Program).Distinct().Order().ToListAsync(ct);
        return new() { Query = q, Students = rows.Select(Profile).ToList(), Colleges = colleges, Programs = programs, Total = total, TotalPages = pages };
    }
    public static void ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length is < 15 or > 128) throw new ValidationException("Set a password of 15 to 128 characters.");
    }
    private void Validate(StudentProfileInput input, bool requireBirth)
    {
        input.StudentId = input.StudentId.Trim(); input.Email = input.Email.Trim(); input.FirstName = input.FirstName.Trim(); input.LastName = input.LastName.Trim(); input.MiddleName = Limit(input.MiddleName);
        Validator.ValidateObject(input, new ValidationContext(input), true);
        if (requireBirth && input.BirthDate is null) throw new ValidationException("Birthdate is required when adding a student.");
        if (input.BirthDate is { } birth && (birth.Year < 1900 || birth >= DateOnly.FromDateTime(clock.Now))) throw new ValidationException("Choose a valid birthdate from 1900 through yesterday.");
        if (!Genders.Contains(input.Gender) || !CreateEventInput.Branches.Contains(input.Campus)) throw new ValidationException("Choose a supported gender and campus.");
        var program = CreateEventInput.ProgramOptions.FirstOrDefault(p => p.Department == input.College && (string.Equals(p.Code, input.Program, StringComparison.OrdinalIgnoreCase) || string.Equals("BS " + p.Name, input.Program, StringComparison.OrdinalIgnoreCase) || p.Code == "BECEd" && input.Program == "Bachelor of Early Childhood Education"));
        if (program.Code is null) throw new ValidationException("Choose a program belonging to the selected college.");
        input.Program = program.Code;
    }
    public async Task CreateAsync(StudentProfileInput input, CancellationToken ct, bool imported = false)
    {
        Validate(input, !imported); ValidatePassword(input.Password);
        if (await db.Students.AnyAsync(s => s.StudentId == input.StudentId, ct) || await db.Users.AnyAsync(u => u.Email == input.Email, ct)) throw new ValidationException("Student ID or email already belongs to an existing record.");
        var user = new User { Email = input.Email, Role = "Student", IsActive = input.Active, PasswordHash = "", PasswordSalt = "" };
        user.PasswordHash = hasher.HashPassword(user, input.Password!);
        var student = new Student { StudentId = input.StudentId, FirstName = input.FirstName, MiddleName = input.MiddleName, LastName = input.LastName, BirthDate = input.BirthDate,
            Gender = input.Gender, CampusBranch = input.Campus, Department = input.College, Program = input.Program, User = user };
        db.Students.Add(student);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); throw new ValidationException("Student ID or email could not be saved. It may already be in use; reload and try again."); }
    }
    public async Task UpdateAsync(string id, StudentProfileInput input, string operation, CancellationToken ct)
    {
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var student = await db.Students.AsTracking().Include(s => s.User).SingleOrDefaultAsync(s => s.StudentId == id && s.User.Role == "Student", ct) ?? throw new KeyNotFoundException();
        try { if (!CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(_protector.Unprotect(input.Token ?? "")), Snapshot(student))) throw new CryptographicException(); }
        catch (Exception ex) when (ex is CryptographicException or FormatException) { throw new ValidationException("This student changed. Reload the directory and review the latest profile before saving."); }
        if (operation == "edit")
        {
            if (input.StudentId != id) throw new ValidationException("Student ID cannot be changed.");
            Validate(input, false);
            if (await db.Users.AnyAsync(u => u.UserId != student.UserId && u.Email == input.Email, ct)) throw new ValidationException("Email already belongs to another account.");
            student.FirstName = input.FirstName; student.MiddleName = input.MiddleName; student.LastName = input.LastName; student.BirthDate = input.BirthDate;
            student.Gender = input.Gender; student.CampusBranch = input.Campus; student.Department = input.College; student.Program = input.Program;
            student.User.Email = input.Email; student.User.IsActive = input.Active;
        }
        else if (operation == "access") student.User.IsActive = input.Active;
        else if (operation == "password") { ValidatePassword(input.Password); student.User.PasswordHash = hasher.HashPassword(student.User, input.Password!); student.User.PasswordSalt = ""; }
        else throw new ValidationException("Unknown student operation.");
        await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct);
    }
    public async Task<byte[]> ExportAsync(StudentDirectoryQuery query, CancellationToken ct)
    {
        var rows = await Filter(query).Include(s => s.User).OrderBy(s => s.StudentId).Take(10001).ToListAsync(ct);
        if (rows.Count > 10000) throw new ValidationException("Narrow the filters to export at most 10,000 students.");
        static string Cell(string? value) { value ??= ""; if (value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@' || value.StartsWith('\t') || value.StartsWith('\r')) value = "'" + value; return "\"" + value.Replace("\"", "\"\"") + "\""; }
        var csv = new StringBuilder("StudentId,FullName,FirstName,MiddleName,LastName,Gender,CampusBranch,Department,Program,Email,BirthDate(MM/dd/yyyy),AccountStatus\r\n");
        foreach (var s in rows) csv.AppendLine(string.Join(",", new[] { s.StudentId, string.Join(" ", new[] { s.FirstName, s.MiddleName, s.LastName }.Where(v => !string.IsNullOrWhiteSpace(v))), s.FirstName, s.MiddleName, s.LastName, s.Gender, s.CampusBranch, s.Department, s.Program, s.User.Email, s.BirthDate?.ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture), s.User.IsActive ? "Active" : "Inactive" }.Select(Cell)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }
}
