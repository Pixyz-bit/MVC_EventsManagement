namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed class StudentDirectoryViewModel
{
    public required StudentDirectoryQuery Query { get; init; }
    public required IReadOnlyList<StudentProfileData> Students { get; init; }
    public required IReadOnlyList<string> Colleges { get; init; }
    public required IReadOnlyDictionary<string, string[]> ProgramColleges { get; init; }
    public int Total { get; init; }
    public int TotalPages { get; init; }
    public StudentProfileInput Input { get; set; } = new();
    public string? Reopen { get; set; }
    public StudentImportReport? ImportReport { get; set; }
}
