namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed class EventAnalyticsQuery
{
    public int Interval { get; set; } = 15;
    public int ArrivalPage { get; set; } = 1;
    public string Dimension { get; set; } = "Program";
    public string? Cohort { get; set; }
    public string? Q { get; set; }
    public string? College { get; set; }
    public string? Program { get; set; }
    public int Page { get; set; } = 1;
    public string? From { get; set; }
}
