using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Data;
using MVC_QCUEventsManagement.Models;
using MVC_QCUEventsManagement.ViewModels.Student;

namespace MVC_QCUEventsManagement.Services;

public sealed class StudentPortalService(ApplicationDbContext db, EventClock clock, IPasswordHasher<User> hasher, AccountService accounts)
{
    public async Task<StudentProfileViewModel?> ProfileAsync(int userId, CancellationToken ct)
    {
        var student = await db.Students.AsNoTracking().Include(s => s.User)
            .SingleOrDefaultAsync(s => s.UserId == userId, ct);
        return student is null ? null : Profile(student);
    }

    public async Task<StudentBookingViewModel?> BookingAsync(int userId, int eventId, CancellationToken ct)
    {
        var student = await db.Students.AsNoTracking().Include(s => s.User).SingleOrDefaultAsync(s => s.UserId == userId, ct);
        var ev = await db.Events.AsNoTracking().Include(e => e.Sponsors).SingleOrDefaultAsync(e => e.EventId == eventId, ct);
        if (student is null || ev is null) return null;
        var now = clock.Now;
        if (!StudentDashboardService.Eligible(ev, student) || ev.Status != "Upcoming" || ev.EventEnd <= now) return null;
        return new(Profile(student), StudentDashboardService.Card(ev, null, now), ev.TargetYearLevel);
    }

    public Task<int?> ActivePassIdAsync(int userId, int eventId, CancellationToken ct) =>
        db.EventRegistrations.AsNoTracking()
            .Where(r => r.EventId == eventId && r.Student.UserId == userId && r.Status != "Cancelled")
            .Select(r => (int?)r.EventRegistrationId).SingleOrDefaultAsync(ct);

    public async Task<int> BookAsync(int userId, int eventId, StudentBookingInput input, CancellationToken ct)
    {
        var student = await db.Students.Include(s => s.User)
            .SingleOrDefaultAsync(s => s.UserId == userId && s.User.IsActive && s.User.Role == "Student", ct)
            ?? throw new InvalidOperationException("Your student profile is unavailable.");
        var ev = await db.Events.SingleOrDefaultAsync(e => e.EventId == eventId, ct)
            ?? throw new InvalidOperationException("This event is unavailable.");
        var now = clock.Now;
        var today = DateOnly.FromDateTime(now);
        if (ev.Status != "Upcoming" || ev.EventEnd <= now || today < ev.RegStart || today > ev.RegEnd)
            throw new InvalidOperationException("Registration is closed for this event.");
        if (!StudentDashboardService.Eligible(ev, student) || (ev.TargetYearLevel.HasValue && ev.TargetYearLevel != input.Year))
            throw new InvalidOperationException("This event is restricted to a different academic group.");
        if (ev.CurrentRegistrations >= ev.MaxCapacity)
            throw new InvalidOperationException("This event has reached capacity.");
        if (await ActivePassIdAsync(userId, eventId, ct) is not null)
            throw new InvalidOperationException("You already have a pass for this event.");

        var registration = new EventRegistration
        {
            EventId = eventId, StudentId = student.StudentId, CurrentYearLvl = input.Year,
            CurrentSection = input.Section.Trim()
        };
        db.EventRegistrations.Add(registration);
        ev.CurrentRegistrations++;
        await SaveBookingAsync(ct);
        return registration.EventRegistrationId;
    }

    public async Task CancelAsync(int userId, int registrationId, CancellationToken ct)
    {
        var registration = await db.EventRegistrations.Include(r => r.Event)
            .SingleOrDefaultAsync(r => r.EventRegistrationId == registrationId &&
                r.Student.UserId == userId && r.Student.User.IsActive && r.Student.User.Role == "Student", ct);
        if (registration is null || !CanCancel(registration.Event, registration, clock.Now))
            throw new InvalidOperationException("This reservation can no longer be cancelled.");
        registration.Status = "Cancelled";
        registration.Event.CurrentRegistrations = Math.Max(0, registration.Event.CurrentRegistrations - 1);
        db.Entry(registration.Event).Property(e => e.CurrentRegistrations).IsModified = true;
        await SaveBookingAsync(ct);
    }

    public async Task<StudentPassViewModel?> PassAsync(int userId, int registrationId, bool justBooked, CancellationToken ct)
    {
        var registration = await db.EventRegistrations.AsNoTracking().Include(r => r.Student).ThenInclude(s => s.User)
            .Include(r => r.Event).ThenInclude(e => e.Sponsors)
            .SingleOrDefaultAsync(r => r.EventRegistrationId == registrationId && r.Student.UserId == userId, ct);
        if (registration is null) return null;

        var ev = registration.Event;
        var now = clock.Now;
        var active = registration.Status == "NoShow" && ev.Status == "Upcoming" && ev.EventEnd > now;
        var (status, notice) = (ev.Status, registration.Status) switch
        {
            ("Cancelled", _) => ("Event cancelled", ev.CancellationReason ?? "The event has been cancelled."),
            (_, "Cancelled") => ("Reservation cancelled", "This reservation was cancelled."),
            (_, "Present") => ("Checked in", "Attendance has already been recorded. This pass cannot be scanned again."),
            _ when active => ("Registered", null),
            _ => ("Event ended", "This event has ended and the pass is inactive.")
        };
        return new(Profile(registration.Student), StudentDashboardService.Card(ev, registration, now),
            registration.CurrentYearLvl, registration.CurrentSection, status, active, notice, active && justBooked);
    }

    public async Task<bool> ChangePasswordAsync(int userId, StudentPasswordInput input, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == userId && u.IsActive && u.Role == "Student", ct);
        // Authenticate against the same hash verifier used for sign-in, including legacy invalid-hash handling.
        if (user is null || (await accounts.AuthenticateAsync(user.Email, input.CurrentPassword, ct))?.UserId != userId)
            return false;
        // Match the hash snapshot before replacing it to avoid overwriting a concurrent reset.
        var newHash = hasher.HashPassword(user, input.NewPassword);
        return await db.Users.Where(u => u.UserId == userId && u.IsActive && u.Role == "Student" &&
                EF.Functions.Collate(u.PasswordHash, "Latin1_General_100_BIN2") == user.PasswordHash)
            .ExecuteUpdateAsync(update => update.SetProperty(u => u.PasswordHash, newHash)
                .SetProperty(u => u.PasswordSalt, ""), ct) == 1;
    }

    internal static bool CanCancel(Event ev, EventRegistration registration, DateTime now) =>
        registration.Status == "NoShow" && ev.Status == "Upcoming" && ev.EventEnd > now &&
        DateOnly.FromDateTime(now) <= ev.RegEnd;

    private static StudentProfileViewModel Profile(Student student) => new(
        new(string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part))), student.StudentId),
        student.User.Email, student.FirstName, student.MiddleName, student.LastName,
        student.Gender, student.CampusBranch, student.Department, student.Program);

    private async Task SaveBookingAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new InvalidOperationException("Availability changed. Refresh and try again.");
        }
    }
}
