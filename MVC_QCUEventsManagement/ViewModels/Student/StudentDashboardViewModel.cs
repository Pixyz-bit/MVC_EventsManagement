namespace MVC_QCUEventsManagement.ViewModels.Student;

public sealed record StudentDashboardViewModel(string Email, string DisplayName, string? StudentId,
    IReadOnlyList<StudentDashboardEvent> Available, IReadOnlyList<StudentDashboardEvent> Registered);
