namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record AttendanceItem(int RegistrationId, string StudentId, string Name, string College,
    string Program, int Year, string Section, DateTime? CheckedInAt)
{
    public string TicketReference(int eventId) => $"TCK-{eventId:D4}-{RegistrationId:D5}";
}
