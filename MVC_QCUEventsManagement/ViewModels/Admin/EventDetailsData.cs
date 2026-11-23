namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record EventDetailsData(int Id, string Title, string? Description, string Venue, int Capacity,
    int Registered, DateTime Start, DateTime End, DateOnly RegistrationOpen, DateOnly RegistrationDeadline,
    string Status, string? CancellationReason, string? Campus, string? College, string? Programs, int? Year,
    string? Banner, byte[] Version, IReadOnlyList<string> Sponsors);
