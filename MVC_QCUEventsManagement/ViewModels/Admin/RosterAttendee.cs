namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record RosterAttendee(int RegistrationId, string StudentId, string Name, string Campus,
    string College, string Program, int Year, string Section, string Status, byte[] Version)
{
    public string DisplayStatus => Status == "NoShow" ? "Reserved" : Status;
    public string TicketReference(int eventId) => $"TCK-{eventId:D4}-{RegistrationId:D5}";
}
