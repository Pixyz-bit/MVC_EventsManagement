using System.ComponentModel.DataAnnotations;

namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed class StudentProfileInput
{
    [Required, StringLength(50)] public string StudentId { get; set; } = "";
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = "";
    [Required, StringLength(100)] public string FirstName { get; set; } = "";
    [StringLength(100)] public string? MiddleName { get; set; }
    [Required, StringLength(100)] public string LastName { get; set; } = "";
    public DateOnly? BirthDate { get; set; }
    [Required, StringLength(20)] public string Gender { get; set; } = "Prefer not to say";
    [Required, StringLength(100)] public string Campus { get; set; } = "San Bartolome";
    [Required, StringLength(100)] public string College { get; set; } = "College of Computer Studies";
    [Required, StringLength(100)] public string Program { get; set; } = "BSIT";
    public bool Active { get; set; } = true;
    public string? Password { get; set; }
    public string? Token { get; set; }
}
