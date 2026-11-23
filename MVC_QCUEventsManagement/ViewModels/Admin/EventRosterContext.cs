namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record EventRosterContext(int Id, string Title, string Venue, DateTime StartsAt, int Capacity,
    int Registered, string Status, byte[] Version, DateTime EndsAt);
