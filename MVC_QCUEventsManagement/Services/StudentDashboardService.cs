using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Data;
using MVC_QCUEventsManagement.Models;
using MVC_QCUEventsManagement.ViewModels.Admin;
using MVC_QCUEventsManagement.ViewModels.Student;

namespace MVC_QCUEventsManagement.Services;

public sealed class StudentDashboardService(ApplicationDbContext db, EventClock clock)
{
    private async Task<Student?> GetStudentAsync(int userId, CancellationToken ct) =>
        await db.Students.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == userId, ct);

    public async Task<StudentDashboardViewModel> GetAsync(int userId, string email, CancellationToken ct)
    {
        var student = await GetStudentAsync(userId, ct);
        if (student is null) return new(email, email, null, [], []);

        var name = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
        var now = clock.Now;
        var registrations = await db.EventRegistrations.AsNoTracking()
            .Where(r => r.StudentId == student.StudentId && r.Status != "Cancelled").ToDictionaryAsync(r => r.EventId, ct);
        var owned = registrations.Keys.ToArray();
        var events = await db.Events.AsNoTracking().Include(e => e.Sponsors).AsSplitQuery()
            .Where(e => owned.Contains(e.EventId) || (e.Status == "Upcoming" && e.EventEnd > now))
            .OrderBy(e => e.EventStart).ThenBy(e => e.EventId).ToListAsync(ct);
        var available = events.Where(e => !registrations.ContainsKey(e.EventId) && Eligible(e, student))
            .Select(e => Card(e, null, now))
            .OrderBy(e => e.Status switch { "Open" => 0, "Opens soon" => 1, _ => 2 }).ToList();
        var registered = events.Where(e => registrations.ContainsKey(e.EventId))
            .Select(e => Card(e, registrations[e.EventId], now))
            .OrderBy(e => e.Status is "Completed" or "Cancelled").ToList();
        return new(email, name, student.StudentId, available, registered);
    }

    public async Task<string?> GetBannerAsync(int userId, int eventId, CancellationToken ct)
    {
        var student = await GetStudentAsync(userId, ct);
        if (student is null) return null;
        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == eventId, ct);
        if (ev is null || string.IsNullOrWhiteSpace(ev.EventPhotoPath)) return null;
        if (ev.Status == "Upcoming" && ev.EventEnd > clock.Now && Eligible(ev, student)) return ev.EventPhotoPath;
        return await db.EventRegistrations.AnyAsync(r => r.EventId == eventId && r.StudentId == student.StudentId && r.Status != "Cancelled", ct)
            ? ev.EventPhotoPath : null;
    }

    private static bool Eligible(Event ev, Student student)
    {
        static bool Matches(string? restriction, string value) =>
            string.IsNullOrWhiteSpace(restriction) || string.Equals(restriction.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase);
        return Matches(ev.TargetBranch, student.CampusBranch) && Matches(ev.TargetDepartment, student.Department) &&
            (string.IsNullOrWhiteSpace(ev.TargetProgram) ||
                ev.TargetProgram.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Any(program => ProgramKey(program) == ProgramKey(student.Program)));
    }

    private static string ProgramKey(string value)
    {
        static string Compact(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        var key = Compact(value);
        foreach (var option in CreateEventInput.ProgramOptions)
        {
            if (key == Compact(option.Code) || key == Compact("BS " + option.Name) || key == Compact(option.Name)) return Compact(option.Code);
        }
        return key == "BACHELOROFEARLYCHILDHOODEDUCATION" ? "BECED" : key;
    }

    private static StudentDashboardEvent Card(Event ev, EventRegistration? registration, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var seats = Math.Max(0, ev.MaxCapacity - ev.CurrentRegistrations);
        var status = ev.Status == "Cancelled" ? "Cancelled"
            : registration is not null ? ev.Status == "Completed" || ev.EventEnd <= now ? "Completed" : registration.Status == "Present" ? "Checked in" : "Registered"
            : seats == 0 || ev.RegEnd < today ? "Closed" : ev.RegStart > today ? "Opens soon" : "Open";
        return new(ev.EventId, ev.Title, ev.Description, ev.VenueLocation, ev.EventStart, ev.EventEnd, ev.RegStart, ev.RegEnd,
            seats, status, Category(ev), ev.CancellationReason, !string.IsNullOrWhiteSpace(ev.EventPhotoPath),
            ev.Sponsors.OrderBy(s => s.SponsorEntryId).Select(s => s.SponsorName).ToList(),
            registration is null ? null : $"TCK-{ev.EventId:D4}-{registration.EventRegistrationId:D5}", registration?.Status == "Present");
    }

    private static string Category(Event ev)
    {
        var text = (ev.Title + " " + ev.Description).ToLowerInvariant();
        bool Has(params string[] terms) => terms.Any(term => text.Contains(term, StringComparison.Ordinal));
        if (Has("hack", "cyber", "security", "code")) return "Hackathon";
        if (Has("workshop", "lab", "cloud", "ai ")) return "Workshop";
        if (Has("sport", "fest", "game", "tournament")) return "Sports fest";
        if (Has("org", "fair", "club", "expo")) return "Org fair";
        return "Seminar";
    }
}
