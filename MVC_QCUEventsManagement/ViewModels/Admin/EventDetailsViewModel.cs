namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed class EventDetailsViewModel
{
    public required EventDetailsData Event { get; init; }
    public required CreateEventInput Input { get; init; }
    public bool Editing { get; init; }
    public bool CanManage { get; init; }
}
