using System.ComponentModel.DataAnnotations;

namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed class CreateEventInput : IValidatableObject
{
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [StringLength(10000)] public string? Description { get; set; }
    [Required, StringLength(200)] public string VenueLocation { get; set; } = "";
    [Range(1, int.MaxValue)] public int MaxCapacity { get; set; } = 150;
    [Required, DataType(DataType.Date)] public DateOnly? EventDate { get; set; }
    [Required, DataType(DataType.Time)] public TimeOnly? StartTime { get; set; }
    [Required, DataType(DataType.Time)] public TimeOnly? EndTime { get; set; }
    [Required] public DateTime? RegStart { get; set; }
    [Required] public DateTime? RegEnd { get; set; }
    [StringLength(100)] public string? TargetBranch { get; set; }
    [StringLength(100)] public string? TargetDepartment { get; set; }
    public List<string> Programs { get; set; } = [];
    [Range(1, 5)] public int? TargetYearLevel { get; set; }
    public List<string> Sponsors { get; set; } = [];
    public IFormFile? Banner { get; set; }
    public bool Confirm { get; set; }

    public static readonly string[] Branches = ["San Bartolome", "Batasan", "San Francisco"];
    public static readonly (string Code, string Name, string Department)[] ProgramOptions =
    [
        ("BSIT", "Information Technology", "College of Computer Studies"),
        ("BSCS", "Computer Science", "College of Computer Studies"),
        ("BSIS", "Information Systems", "College of Computer Studies"),
        ("BSIE", "Industrial Engineering", "College of Engineering"),
        ("BSECE", "Electronics Engineering", "College of Engineering"),
        ("BSA", "Accountancy", "College of Business Administration and Accountancy"),
        ("BSBA", "Business Administration", "College of Business Administration and Accountancy"),
        ("BSEntrep", "Entrepreneurship", "College of Business Administration and Accountancy"),
        ("BECEd", "Early Childhood Education", "College of Education")
    ];
    public static readonly string[] SponsorPresets = ["QCU Alumni Association", "DOST-NCR", "AWS Educate", "Google Developer Student Clubs", "Microsoft Learn"];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EventDate is { } day && (day.Year < 1753 || day.Year > 9998))
            yield return new("Choose an event date between 1753 and 9998.", [nameof(EventDate)]);
        if (StartTime is { } start && EndTime is { } end && start >= end)
            yield return new("The end time must be after the start time on the same day.", [nameof(EndTime)]);
        if (RegStart is { } open && RegEnd is { } close && open >= close)
            yield return new("Registration must close after it opens.", [nameof(RegEnd)]);
        foreach (var (date, field) in new[] { (RegStart, nameof(RegStart)), (RegEnd, nameof(RegEnd)) })
        {
            if (date is not { } value) continue;
            if (value.Year < 1753 || value.Year > 9998)
                yield return new("Choose a registration date between 1753 and 9998.", [field]);
            if (EventDate is { } eventDay && DateOnly.FromDateTime(value) >= eventDay)
                yield return new("Registration dates must be before the event day.", [field]);
        }
        if (!string.IsNullOrWhiteSpace(TargetBranch) && !Branches.Contains(TargetBranch))
            yield return new("Choose a supported campus.", [nameof(TargetBranch)]);
        if (!string.IsNullOrWhiteSpace(TargetDepartment) && !ProgramOptions.Any(p => p.Department == TargetDepartment))
            yield return new("Choose a supported college.", [nameof(TargetDepartment)]);
        if (Programs.Count > ProgramOptions.Length || Programs.Any(code => !ProgramOptions.Any(p => p.Code == code &&
            (string.IsNullOrWhiteSpace(TargetDepartment) || p.Department == TargetDepartment))))
            yield return new("Choose programs belonging to the selected college.", [nameof(Programs)]);
        if (Sponsors.Count > 20 || Sponsors.Any(name => string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150))
            yield return new("Add up to 20 sponsors, each with a name of 1 to 150 characters.", [nameof(Sponsors)]);
        if (!Confirm) yield return new("Confirm the event details before publishing.", [nameof(Confirm)]);
    }
}
