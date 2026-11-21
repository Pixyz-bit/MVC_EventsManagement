namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record EventListItem(
    string Title,
    string Venue,
    string? Department,
    DateTime StartsAt,
    DateTime RegistrationOpensAt,
    DateTime RegistrationClosesAt,
    int Registered,
    int Capacity,
    RegistrationStatus Status);
