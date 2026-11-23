using System.ComponentModel.DataAnnotations;

namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed class ManagedAccountInput
{
    public int Id { get; set; }
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = "";
    [Required] public string Role { get; set; } = "";
    public bool Active { get; set; }
    public string? Password { get; set; }
    public string? Version { get; set; }
    public string? StudentId { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? Campus { get; set; }
    public string? College { get; set; }
    public string? Program { get; set; }
    public string? Gender { get; set; }
    public DateOnly? BirthDate { get; set; }
    public bool HasStudentProfile { get; set; }
}
