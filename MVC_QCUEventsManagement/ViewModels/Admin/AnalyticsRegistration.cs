namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record AnalyticsRegistration(int Id, string StudentId, string Name, string Campus, string College,
    string Program, int Year, string Section, string Status, DateTime? CheckedInAt)
{
    public string TicketReference(int eventId) => $"TCK-{eventId:D4}-{Id:D5}";
}
