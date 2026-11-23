using System.ComponentModel.DataAnnotations;

namespace MVC_QCUEventsManagement.ViewModels.Student;

public sealed record StudentIdentity(string Name, string? Id)
{
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Take(2).Select(part => char.ToUpperInvariant(part[0])));
}

public sealed record StudentProfileViewModel(StudentIdentity Identity, string Email, string FirstName,
    string? MiddleName, string LastName, string Gender, string Campus, string Department, string Program);

public sealed record StudentBookingViewModel(StudentProfileViewModel Profile, StudentDashboardEvent Event, int? TargetYear);

public sealed record StudentPassViewModel(StudentProfileViewModel Profile, StudentDashboardEvent Event,
    int Year, string Section, string Status, bool Active, string? Notice, bool JustBooked);

public sealed class StudentBookingInput
{
    [Range(1, 5, ErrorMessage = "Select your current year.")]
    public int Year { get; set; }

    [Required, StringLength(50)]
    public string Section { get; set; } = "";

    public bool Terms { get; set; }
}

public sealed class StudentPasswordInput
{
    [Required, StringLength(128), DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = "";

    [Required, StringLength(128, MinimumLength = 15), DataType(DataType.Password)]
    public string NewPassword { get; set; } = "";

    [Required, Compare(nameof(NewPassword)), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}
