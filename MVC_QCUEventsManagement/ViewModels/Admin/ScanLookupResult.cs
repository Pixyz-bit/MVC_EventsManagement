namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record ScanLookupResult(string State, string Message, ScanAttendee? Attendee = null, int? CheckedInCount = null)
{
    public bool CanConfirm => State == "ValidPending";
}
