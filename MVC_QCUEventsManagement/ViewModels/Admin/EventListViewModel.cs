namespace MVC_QCUEventsManagement.ViewModels.Admin;

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
