namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record StudentProfileData(string StudentId, string FirstName, string? MiddleName, string LastName,
    string Email, DateOnly? BirthDate, string Gender, string Campus, string College, string Program, bool Active, string Token)
{
    public string Name => string.Join(" ", new[] { FirstName, MiddleName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
