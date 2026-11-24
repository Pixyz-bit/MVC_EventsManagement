using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
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

public sealed class AccountManagementService(ApplicationDbContext db, StudentService students, IPasswordHasher<User> hasher, IDataProtectionProvider protection)
{
    private const int PageSize = 25;
    private readonly IDataProtector _versions = protection.CreateProtector("Qcu.AccountManagement.v1");

    public async Task CreateAsync(CreateAccountInput input, int actorId, CancellationToken ct)
    {
        input.Email = input.Email.Trim();
        Validator.ValidateObject(input, new ValidationContext(input), true);
        if (input.Role is not ("Admin" or "Student")) throw new ValidationException("Choose Student or Administrator.");
        StudentService.ValidatePassword(input.Password);
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (!await db.Users.AnyAsync(u => u.UserId == actorId && u.Role == "Admin" && u.IsActive, ct))
            throw new ValidationException("Your administrator access changed. Sign in again.");
        if (input.Role == "Student")
            await students.CreateAsync(input.StudentProfile(), ct);
        else
        {
            if (await db.Users.AnyAsync(u => u.Email == input.Email, ct))
                throw new ValidationException("Email already belongs to another account.");
            var user = new User { Email = input.Email, Role = "Admin", IsActive = input.Active, PasswordHash = "", PasswordSalt = "" };
            user.PasswordHash = hasher.HashPassword(user, input.Password!);
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
        }
        if (transaction is not null) await transaction.CommitAsync(ct);
    }

    private static IQueryable<User> Filter(IQueryable<User> rows, string q, string role, string status)
    {
        if (q.Length > 0)
            rows = rows.Where(u => u.Email.Contains(q) || u.Student != null &&
                (u.Student.StudentId.Contains(q) || (u.Student.FirstName + " " + (u.Student.MiddleName == null || u.Student.MiddleName == "" ? "" : u.Student.MiddleName + " ") + u.Student.LastName).Contains(q)));
        if (role is "Admin" or "Student") rows = rows.Where(u => u.Role == role);
        if (status is "Active" or "Inactive") rows = rows.Where(u => u.IsActive == (status == "Active"));
        return rows;
    }

    public async Task<AccountManagementViewModel> ListAsync(string? q, string? role, string? status, int page, int actorId, CancellationToken ct)
    {
        var query = Normalize(q);
        role = role is "Admin" or "Student" ? role : "";
        status = status is "Active" or "Inactive" ? status : "";
        var users = db.Users.AsNoTracking();
        var summary = await users.GroupBy(_ => 1).Select(g => new
        {
            Total = g.Count(), Admins = g.Count(u => u.IsActive && u.Role == "Admin"),
            Students = g.Count(u => u.IsActive && u.Role == "Student"), Inactive = g.Count(u => !u.IsActive)
        }).SingleOrDefaultAsync(ct);
        var matching = Filter(users, query, role, status);
        var count = await matching.CountAsync(ct);
        var pages = Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
        page = Math.Clamp(page, 1, pages);
        var rows = await matching.OrderBy(u => u.Email).ThenBy(u => u.UserId).Skip((page - 1) * PageSize).Take(PageSize)
            .Select(u => new ManagedAccountRow(u.UserId, u.Email, u.Role, u.IsActive, u.Student == null ? null : u.Student.StudentId,
                u.Student == null ? null : u.Student.FirstName + " " + u.Student.LastName)).ToListAsync(ct);
        return new(rows, summary?.Total ?? 0, summary?.Admins ?? 0, summary?.Students ?? 0, summary?.Inactive ?? 0, count, page, pages, query, role, status, actorId);
    }

    private static string Normalize(string? value)
    {
        var trimmed = value?.Trim() ?? "";
        return trimmed[..Math.Min(trimmed.Length, 150)];
    }

    private static string Version(User u)
    {
        var s = u.Student;
        var data = new object?[] { u.UserId, u.Email, u.Role, u.IsActive, u.PasswordHash, s?.StudentId, s?.FirstName, s?.MiddleName,
            s?.LastName, s?.Gender, s?.BirthDate, s?.CampusBranch, s?.Department, s?.Program };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(data)));
    }

    public async Task<ManagedAccountInput> GetAsync(int id, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().Include(u => u.Student).SingleOrDefaultAsync(u => u.UserId == id, ct)
            ?? throw new KeyNotFoundException();
        var s = u.Student;
        return new()
        {
            Id = u.UserId, Email = u.Email, Role = u.Role, Active = u.IsActive, Version = _versions.Protect(Version(u)),
            StudentId = s?.StudentId, FirstName = s?.FirstName, MiddleName = s?.MiddleName, LastName = s?.LastName,
            BirthDate = s?.BirthDate, HasStudentProfile = s is not null, Gender = s?.Gender,
            Campus = s?.CampusBranch, College = s?.Department, Program = s?.Program
        };
    }

    public async Task SaveAsync(ManagedAccountInput input, int actorId, CancellationToken ct)
    {
        input.Email = input.Email.Trim();
        Validator.ValidateObject(input, new ValidationContext(input), true);
        if (input.Role is not ("Admin" or "Student")) throw new ValidationException("Choose a valid role.");
        if (!string.IsNullOrEmpty(input.Password)) StudentService.ValidatePassword(input.Password);
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (!await db.Users.AnyAsync(u => u.UserId == actorId && u.Role == "Admin" && u.IsActive, ct))
            throw new ValidationException("Your administrator access changed. Sign in again.");
        var u = await db.Users.AsTracking().Include(u => u.Student).SingleOrDefaultAsync(u => u.UserId == input.Id, ct)
            ?? throw new KeyNotFoundException();
        string? version = null;
        try { version = _versions.Unprotect(input.Version ?? ""); } catch (CryptographicException) { }
        if (version != Version(u)) throw new ValidationException("The account changed. Reopen it to review the latest information.");
        if (u.UserId == actorId && (input.Role != "Admin" || !input.Active))
            throw new ValidationException("Your account must remain an active administrator.");
        if (u.Role == "Admin" && u.IsActive && (input.Role != "Admin" || !input.Active) &&
            !await db.Users.AnyAsync(a => a.UserId != u.UserId && a.Role == "Admin" && a.IsActive, ct))
            throw new ValidationException("At least one active administrator must remain.");
        if (await db.Users.AnyAsync(a => a.UserId != u.UserId && a.Email == input.Email, ct))
            throw new ValidationException("Email already belongs to another account.");

        if (input.Role == "Student" && StudentFieldsChanged(u.Student, input))
        {
            var profile = new StudentProfileInput
            {
                StudentId = u.Student?.StudentId ?? input.StudentId ?? "", Email = input.Email,
                FirstName = input.FirstName ?? "", MiddleName = input.MiddleName, LastName = input.LastName ?? "",
                Campus = input.Campus ?? "", College = input.College ?? "", Program = input.Program ?? "", Gender = input.Gender ?? ""
            };
            students.Validate(profile, requireBirth: false);
            if (u.Student is null)
            {
                if (await db.Students.AnyAsync(s => s.StudentId == profile.StudentId, ct))
                    throw new ValidationException("Student ID already belongs to another profile.");
                u.Student = new Student { StudentId = profile.StudentId, FirstName = profile.FirstName, LastName = profile.LastName,
                    Gender = profile.Gender, CampusBranch = profile.Campus, Department = profile.College, Program = profile.Program, User = u };
            }
            u.Student.FirstName = profile.FirstName;
            u.Student.MiddleName = profile.MiddleName;
            u.Student.LastName = profile.LastName;
            u.Student.Gender = profile.Gender;
            u.Student.CampusBranch = profile.Campus;
            u.Student.Department = profile.College;
            u.Student.Program = profile.Program;
        }
        u.Email = input.Email;
        u.Role = input.Role;
        u.IsActive = input.Active;
        if (!string.IsNullOrEmpty(input.Password))
        {
            u.PasswordHash = hasher.HashPassword(u, input.Password);
            u.PasswordSalt = "";
        }
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
    }

    private static bool StudentFieldsChanged(Student? s, ManagedAccountInput input) =>
        s is null || s.FirstName != input.FirstName || (s.MiddleName ?? "") != (input.MiddleName ?? "") ||
        s.LastName != input.LastName || s.Gender != input.Gender || s.CampusBranch != input.Campus ||
        s.Department != input.College || s.Program != input.Program;

    public async Task<byte[]> ExportAsync(string? q, string? role, string? status, CancellationToken ct)
    {
        var rows = await Filter(db.Users.AsNoTracking(), Normalize(q), role ?? "", status ?? "").Include(u => u.Student)
            .OrderBy(u => u.UserId).Take(10001).ToListAsync(ct);
        if (rows.Count > 10000) throw new ValidationException("Narrow the filters to export at most 10,000 accounts.");
        var csv = new StringBuilder("UserId,Email,Role,AccountStatus,StudentId,FullName,Gender,CampusBranch,Department,Program\r\n");
        foreach (var u in rows)
        {
            var s = u.Student;
            var name = s is null ? null : string.Join(" ", new[] { s.FirstName, s.MiddleName, s.LastName }.Where(n => !string.IsNullOrWhiteSpace(n)));
            csv.AppendJoin(',', new[] {u.UserId.ToString(CultureInfo.InvariantCulture), u.Email, u.Role, u.IsActive ? "Active" : "Inactive", s?.StudentId,
                name, s?.Gender, s?.CampusBranch, s?.Department, s?.Program }.Select(StudentService.CsvCell)).Append("\r\n");
        }
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }
}
