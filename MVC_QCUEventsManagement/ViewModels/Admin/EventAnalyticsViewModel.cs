namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record EventAnalyticsViewModel(EventRosterContext Event, EventAnalyticsQuery Query,
    int Reserved, int Attended, int Cancelled, IReadOnlyDictionary<string, IReadOnlyList<AnalyticsGroup>> Distributions,
    ArrivalSeries Arrivals, IReadOnlyList<string> Colleges, IReadOnlyList<string> Programs,
    IReadOnlyList<AnalyticsRegistration> CohortRows, int Total, int TotalPages, DateTime? LatestCheckIn = null)
{
    public int Unscanned => Reserved - Attended;
    public double Turnout => Rate(Attended, Reserved);
    public double Occupancy => Rate(Attended, Event.Capacity);
    public double BookingFill => Rate(Event.Registered, Event.Capacity);
    public double Attrition => Rate(Cancelled, Reserved + Cancelled);
    private static double Rate(int numerator, int denominator) => denominator == 0 ? 0 : 100d * numerator / denominator;
}
