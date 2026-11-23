namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed class EventRosterQuery
{
    public string? Q { get; set; }
    public string? College { get; set; }
    public string? Program { get; set; }
    public int? Year { get; set; }
    public string? Cohort { get; set; }
    public int Page { get; set; } = 1;
    public string? From { get; set; }
}
