namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed class UserDirectoryQuery
{
    public string? Q { get; set; }
    public string? Role { get; set; }
    public string? Status { get; set; }
    public string? College { get; set; }
    public string? Program { get; set; }
    public string? Campus { get; set; }
    public int Page { get; set; } = 1;
}
