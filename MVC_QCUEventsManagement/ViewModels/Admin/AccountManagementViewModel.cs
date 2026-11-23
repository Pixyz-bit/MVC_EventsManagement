namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record AccountManagementViewModel(IReadOnlyList<ManagedAccountRow> Rows, int Total, int Admins, int Students,
    int Inactive, int Matches, int Page, int Pages, string Q, string Role, string Status, int ActorId)
{
    public ManagedAccountInput? Editor { get; init; }
}
