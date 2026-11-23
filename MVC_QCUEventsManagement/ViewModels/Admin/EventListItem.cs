namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record EventListItem(
    string Title,
    string Venue,
    string? Department,
    DateTime StartsAt,
    DateOnly RegistrationOpensAt,
    DateOnly RegistrationClosesAt,
    int Registered,
    int Capacity,
    RegistrationStatus Status,
    int EventId = 0);
