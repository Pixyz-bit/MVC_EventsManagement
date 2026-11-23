using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Data;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Services;

public sealed class RegistrationService(ApplicationDbContext db, EventClock clock)
{
    private const int PageSize = 25;

    public async Task<EventRosterViewModel?> GetRosterAsync(int id, EventRosterQuery input, CancellationToken ct)
    {
        var now = clock.Now;
        var ev = await db.Events.AsNoTracking().Where(e => e.EventId == id)
            .Select(e => new EventRosterContext(e.EventId, e.Title, e.VenueLocation, e.EventStart, e.MaxCapacity,
                e.CurrentRegistrations, e.Status == "Upcoming" && e.EventEnd <= now ? "Completed" : e.Status, e.RowVersion)).SingleOrDefaultAsync(ct);
        if (ev is null) return null;
        static string? Limit(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim()[..Math.Min(text.Trim().Length, 150)];
        var query = new EventRosterQuery { Q = Limit(input.Q), College = Limit(input.College), Program = Limit(input.Program),
            Year = input.Year is >= 1 and <= 5 ? input.Year : null,
            Cohort = string.Equals(input.Cohort, "Cancelled", StringComparison.OrdinalIgnoreCase) ? "Cancelled" : null,
            From = input.From == "history" ? "history" : null };
        var registrations = db.EventRegistrations.AsNoTracking().Where(r => r.EventId == id);
        var counts = await registrations.GroupBy(r => r.Status == "Cancelled")
            .Select(g => new { Cancelled = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Cancelled, g => g.Count, ct);
        var colleges = await registrations.Select(r => r.Student.Department).Distinct().OrderBy(c => c).ToListAsync(ct);
        var programs = await registrations.Select(r => r.Student.Program).Distinct().OrderBy(p => p).ToListAsync(ct);
        var years = await registrations.Select(r => r.CurrentYearLvl).Distinct().OrderBy(y => y).ToListAsync(ct);
        var matching = registrations.Where(r => (r.Status == "Cancelled") == (query.Cohort == "Cancelled"));
        if (query.Q is { } q)
            matching = matching.Where(r => EF.Functions.Collate(r.StudentId, "Latin1_General_100_CI_AS").Contains(q)
                || EF.Functions.Collate(r.Student.FirstName + " " + (r.Student.MiddleName == null || r.Student.MiddleName == "" ? "" : r.Student.MiddleName + " ") + r.Student.LastName, "Latin1_General_100_CI_AS").Contains(q));
        if (query.College is { } college) matching = matching.Where(r => r.Student.Department == college);
        if (query.Program is { } program) matching = matching.Where(r => r.Student.Program == program);
        if (query.Year is { } year) matching = matching.Where(r => r.CurrentYearLvl == year);
        var total = await matching.CountAsync(ct);
        var pages = Math.Max(1, (int)Math.Ceiling((double)total / PageSize));
        query.Page = Math.Clamp(input.Page, 1, pages);
        var attendees = await matching.OrderByDescending(r => r.EventRegistrationId).Skip((query.Page - 1) * PageSize).Take(PageSize)
            .Select(r => new RosterAttendee(r.EventRegistrationId, r.StudentId,
                r.Student.FirstName + " " + (r.Student.MiddleName == null || r.Student.MiddleName == "" ? "" : r.Student.MiddleName + " ") + r.Student.LastName,
                r.Student.CampusBranch, r.Student.Department, r.Student.Program, r.CurrentYearLvl, r.CurrentSection, r.Status, r.RowVersion)).ToListAsync(ct);
        return new EventRosterViewModel(ev, query, counts.GetValueOrDefault(false), counts.GetValueOrDefault(true), colleges, programs, years, attendees, total, pages);
    }

    public async Task VoidPassAsync(int eventId, int registrationId, VoidPassInput input, CancellationToken ct)
    {
        Validator.ValidateObject(input, new ValidationContext(input), true);
        if (!input.Confirmed) throw new ValidationException("Confirm that you want to cancel this registration pass.");
        static byte[] Token(string value)
        {
            try { var bytes = Convert.FromBase64String(value); if (bytes.Length == 8) return bytes; }
            catch (FormatException) { }
            throw new ValidationException("Reload the roster before cancelling a pass.");
        }
        var registrationToken = Token(input.Version);
        var eventToken = Token(input.EventVersion);
        var registration = await db.EventRegistrations.AsTracking().Include(r => r.Event)
            .SingleOrDefaultAsync(r => r.EventId == eventId && r.EventRegistrationId == registrationId, ct) ?? throw new KeyNotFoundException();
        if (registration.Status == "Cancelled") throw new ValidationException("This registration pass is already cancelled.");
        db.Entry(registration).Property(r => r.RowVersion).OriginalValue = registrationToken;
        db.Entry(registration.Event).Property(e => e.RowVersion).OriginalValue = eventToken;
        registration.Status = "Cancelled";
        registration.Event.CurrentRegistrations = Math.Max(0, registration.Event.CurrentRegistrations - 1);
        db.Entry(registration.Event).Property(e => e.CurrentRegistrations).IsModified = true;
        // One SaveChanges transaction preserves check-in history and rolls back both updates on stale tokens.
        await db.SaveChangesAsync(ct);
    }
}
