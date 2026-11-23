namespace MVC_QCUEventsManagement.ViewModels.Student;

public sealed record StudentDashboardEvent(int Id, string Title, string? Description, string Venue, DateTime Start, DateTime End,
    DateOnly RegStart, DateOnly RegEnd, int SeatsAvailable, string Status, string Category, string? CancellationReason,
    bool HasBanner, IReadOnlyList<string> Sponsors, string? PassReference, bool CheckedIn,
    int? RegistrationId, bool CanCancel, int Capacity)
{
    public string TimeRange => Start.Date == End.Date ? $"{Start:h:mm tt} to {End:h:mm tt}" : $"{Start:MMM d, h:mm tt} to {End:MMM d, h:mm tt}";

    public string BannerFallback => Category switch
    {
        "Hackathon" => "~/images/student/hero_cyber_ai.jpg",
        "Workshop" => "~/images/student/hero_cloud_lab.jpg",
        "Sports fest" or "Org fair" => "~/images/qcu-campus.png",
        _ => "~/images/student/campus-clean.jpg"
    };
}
