using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Data;
using MVC_QCUEventsManagement.Models;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Services;

public sealed class AttendanceService(ApplicationDbContext db, EventClock clock)
{
    private static readonly Regex Ticket = new(@"^TCK-([0-9]{1,10})-([0-9]{1,10})(?:[|\s].*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public async Task<AttendanceScannerViewModel?> GetScannerAsync(int id, bool fromHistory, CancellationToken ct)
    {
        var now = clock.Now;
        var ev = await db.Events.AsNoTracking().Where(e => e.EventId == id)
            .Select(e => new EventRosterContext(e.EventId, e.Title, e.VenueLocation, e.EventStart, e.MaxCapacity,
                e.CurrentRegistrations, e.Status == "Upcoming" && e.EventEnd <= now ? "Completed" : e.Status, e.RowVersion)).SingleOrDefaultAsync(ct);
        return ev is null ? null : new AttendanceScannerViewModel(ev, fromHistory);
    }

    private static IQueryable<ScanAttendee> Project(IQueryable<EventRegistration> rows) => rows.Select(r => new ScanAttendee(
        r.EventRegistrationId, r.EventId, r.Event.Title,
        r.Student.FirstName + " " + (r.Student.MiddleName == null || r.Student.MiddleName == "" ? "" : r.Student.MiddleName + " ") + r.Student.LastName,
        r.StudentId, r.Student.CampusBranch, r.Student.Department, r.Student.Program, r.CurrentYearLvl, r.CurrentSection,
        r.Status, r.Event.Status, r.Event.EventEnd, r.Event.CancellationReason, r.CheckInTimestamp, r.RowVersion, r.Event.RowVersion));

    public async Task<ScanLookupResult> LookupAsync(int eventId, string? query, CancellationToken ct)
    {
        if (!await db.Events.AsNoTracking().AnyAsync(e => e.EventId == eventId, ct)) throw new KeyNotFoundException();
        var text = query?.Trim() ?? "";
        if (text.Length == 0 || text.Length > 150) return new("NotFound", "Enter a ticket reference, registration number, student ID or email up to 150 characters.");
        var rows = db.EventRegistrations.AsNoTracking();
        int? encodedEvent = null;
        if (text.StartsWith("TCK-", StringComparison.OrdinalIgnoreCase))
        {
            var match = Ticket.Match(text);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var ticketEvent) || ticketEvent <= 0
                || !int.TryParse(match.Groups[2].Value, out var ticketRegistration) || ticketRegistration <= 0)
                return new("NotFound", "The ticket reference is invalid. Check the pass and try again.");
            encodedEvent = ticketEvent;
            rows = rows.Where(r => r.EventRegistrationId == ticketRegistration);
        }
        else
        {
            int.TryParse(text, out var numeric);
            rows = rows.Where(r => r.EventRegistrationId == numeric || EF.Functions.Collate(r.StudentId, "Latin1_General_100_CI_AS") == text
                || EF.Functions.Collate(r.Student.User.Email, "Latin1_General_100_CI_AS") == text);
        }
        var attendee = await Project(rows.Where(r => r.EventId == eventId).OrderBy(r => r.Status == "Cancelled").ThenByDescending(r => r.EventRegistrationId)).FirstOrDefaultAsync(ct)
            ?? await Project(rows.OrderByDescending(r => r.EventRegistrationId)).FirstOrDefaultAsync(ct);
        if (attendee is null) return new("NotFound", "No matching registration found. Check the reference or student ID and try again.");
        if (encodedEvent is { } encoded && encoded != attendee.EventId)
            return new("WrongEventWarning", "The event encoded in this ticket does not match its registration. Check-in is unavailable.", attendee);
        if (attendee.EventId != eventId)
            return new("WrongEventWarning", $"This pass belongs to {attendee.EventTitle} (event {attendee.EventId}), not the selected event.", attendee);
        if (attendee.EventStatus != "Upcoming" || attendee.EventEnd <= clock.Now)
            return new("CancelledWarning", attendee.EventStatus == "Cancelled" ? $"This event was cancelled. {attendee.CancellationReason}" : "This event has ended or is inactive. Check-in is unavailable.", attendee);
        if (attendee.Status == "Present") return new("DuplicateWarning", $"Already checked in at {attendee.CheckInDisplay} (Philippine time). Do not admit this pass again.", attendee);
        if (attendee.Status == "Cancelled") return new("CancelledWarning", "This registration pass was cancelled. Check-in is unavailable.", attendee);
        return new("ValidPending", "Pass found. Review the attendee’s university ID before confirming check-in.", attendee);
    }

    public async Task<ScanLookupResult> ConfirmAsync(int eventId, CheckInInput input, CancellationToken ct)
    {
        Validator.ValidateObject(input, new ValidationContext(input), true);
        if (!input.Confirmed) throw new ValidationException("Confirm check-in after reviewing the attendee details.");
        static byte[] Token(string value)
        {
            try { var bytes = Convert.FromBase64String(value); if (bytes.Length == 8) return bytes; }
            catch (FormatException) { }
            throw new ValidationException("Look up the pass again before confirming check-in.");
        }
        var version = Token(input.Version); var eventVersion = Token(input.EventVersion);
        var now = clock.Now;
        // A single conditional UPDATE is the check-in's atomic decision point. It never changes the booking counter.
        var changed = await db.EventRegistrations.Where(r => r.EventId == eventId && r.EventRegistrationId == input.RegistrationId
                && r.Status == "NoShow" && r.RowVersion == version && r.Event.RowVersion == eventVersion
                && r.Event.Status == "Upcoming" && r.Event.EventEnd > now)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.Status, "Present").SetProperty(r => r.CheckInTimestamp, (DateTime?)now), ct);
        if (changed == 0)
        {
            var latest = await LookupAsync(eventId, input.RegistrationId.ToString(System.Globalization.CultureInfo.InvariantCulture), ct);
            return latest.CanConfirm ? latest with { State = "Changed", Message = "This pass or event changed. Look up the pass again and review the latest details." } : latest;
        }
        var attendee = await Project(db.EventRegistrations.AsNoTracking().Where(r => r.EventRegistrationId == input.RegistrationId && r.EventId == eventId)).SingleAsync(ct);
        var count = await db.EventRegistrations.AsNoTracking().CountAsync(r => r.EventId == eventId && r.Status == "Present", ct);
        return new("CheckedIn", $"Check-in confirmed for {attendee.Name} at {attendee.CheckInDisplay} (Philippine time).", attendee, count);
    }
}
