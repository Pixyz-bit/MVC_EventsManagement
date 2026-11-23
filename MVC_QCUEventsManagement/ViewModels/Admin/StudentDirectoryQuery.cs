namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed class StudentDirectoryQuery
{
    public string? Q { get; set; }
    public string? College { get; set; }
    public string? Program { get; set; }
    public string? Campus { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
}
