namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record EventHistoryViewModel(string Search, int? AcademicYear, string? Outcome,
    IReadOnlyDictionary<string, int> Counts, IReadOnlyList<int> AcademicYears,
    IReadOnlyList<EventHistoryItem> Events, int Page, int TotalPages, int Total)
{
    public int TotalHistorical => Counts.Values.Sum();
}
