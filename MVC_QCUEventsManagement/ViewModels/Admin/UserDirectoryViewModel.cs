namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record UserDirectoryViewModel(IReadOnlyList<ManagedAccountRow> Rows, int Total, int Admins, int Students,
    int Inactive, int Matches, int Page, int Pages, string Q, string Role, string Status, int ActorId)
{
    public ManagedAccountInput? Editor { get; init; }
    public CreateAccountInput NewAccount { get; init; } = new();
    public string? Open { get; init; }
    public StudentImportReport? ImportReport { get; init; }
    public string College { get; init; } = "";
    public string Program { get; init; } = "";
    public string Campus { get; init; } = "";
    public IReadOnlyList<string> Colleges { get; init; } = [];
    public IReadOnlyDictionary<string, string[]> ProgramColleges { get; init; } = new Dictionary<string, string[]>();
    public UserDirectoryQuery Query => new() { Q = Q, Role = Role, Status = Status, Page = Page, College = College, Program = Program, Campus = Campus };
}
