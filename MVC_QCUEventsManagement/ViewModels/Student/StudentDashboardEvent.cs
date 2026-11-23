namespace MVC_QCUEventsManagement.ViewModels.Student;

public sealed record StudentDashboardEvent(int Id, string Title, string? Description, string Venue, DateTime Start, DateTime End,
    DateOnly RegStart, DateOnly RegEnd, int SeatsAvailable, string Status, string Category, string? CancellationReason,
    bool HasBanner, IReadOnlyList<string> Sponsors, string? PassReference, bool CheckedIn);
