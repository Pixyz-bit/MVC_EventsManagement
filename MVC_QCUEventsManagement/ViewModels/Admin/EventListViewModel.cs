namespace MVC_QCUEventsManagement.ViewModels.Admin;

public enum RegistrationStatus
{
    Open,
    Soon,
    Closed
}

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

public sealed record EventListViewModel(
    string Search,
    RegistrationStatus? Status,
    IReadOnlyDictionary<RegistrationStatus, int> Counts,
    IReadOnlyList<EventListItem> Events,
    int Page,
    int TotalPages,
    int MatchingCount)
{
    public int TotalUpcoming => Counts.Values.Sum();
}
