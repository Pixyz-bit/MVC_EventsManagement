namespace MVC_QCUEventsManagement.Models;

public sealed class Student
{
    public required string StudentId { get; set; }
    public required string FirstName { get; set; }
    public string? MiddleName { get; set; }
    public required string LastName { get; set; }
    public required string Gender { get; set; }
    public required string CampusBranch { get; set; }
    public required string Department { get; set; }
    public required string Program { get; set; }
    public int UserId { get; set; }
    public DateOnly? BirthDate { get; set; }

    public User User { get; set; } = null!;
    public ICollection<EventRegistration> Registrations { get; set; } = new List<EventRegistration>();
}
