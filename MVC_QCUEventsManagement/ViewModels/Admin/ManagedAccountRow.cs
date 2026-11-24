namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record ManagedAccountRow(int Id, string Email, string Role, bool Active, string? StudentId, string? Name,
    string? College = null, string? Program = null, string? Campus = null);
