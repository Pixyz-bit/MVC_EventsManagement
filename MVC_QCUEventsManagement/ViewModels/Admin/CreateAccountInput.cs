using System.ComponentModel.DataAnnotations;

namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed class CreateAccountInput
{
    [Required] public string Role { get; set; } = "Student";
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 15)] public string? Password { get; set; }
    public bool Active { get; set; } = true;
    [StringLength(50)] public string? StudentId { get; set; }
    [StringLength(100)] public string? FirstName { get; set; }
    [StringLength(100)] public string? MiddleName { get; set; }
    [StringLength(100)] public string? LastName { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? Gender { get; set; } = "Prefer not to say";
    public string? Campus { get; set; } = "San Bartolome";
    public string? College { get; set; } = "College of Computer Studies";
    public string? Program { get; set; } = "BSIT";

    public StudentProfileInput StudentProfile() => new()
    {
        StudentId = StudentId ?? "", Email = Email, Password = Password, Active = Active,
        FirstName = FirstName ?? "", MiddleName = MiddleName, LastName = LastName ?? "", BirthDate = BirthDate,
        Gender = Gender ?? "", Campus = Campus ?? "", College = College ?? "", Program = Program ?? ""
    };
}
