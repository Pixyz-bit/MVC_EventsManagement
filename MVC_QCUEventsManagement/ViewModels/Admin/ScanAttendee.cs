using System.Globalization;

namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record ScanAttendee(int RegistrationId, int EventId, string EventTitle, string Name, string StudentId,
    string Campus, string College, string Program, int Year, string Section, string Status,
    string EventStatus, DateTime EventEnd, string? CancellationReason, DateTime? CheckInTimestamp, byte[] Version, byte[] EventVersion)
{
    public string TicketReference => FormattableString.Invariant($"TCK-{EventId:D4}-{RegistrationId:D5}");
    public string? CheckInDisplay => CheckInTimestamp?.ToString("MMM d, yyyy, h:mm:ss tt", CultureInfo.InvariantCulture);
}
