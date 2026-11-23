namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record AnalyticsGroup(string Label, int Reserved, int Attended)
{
    public double Turnout => Reserved == 0 ? 0 : 100d * Attended / Reserved;
}
