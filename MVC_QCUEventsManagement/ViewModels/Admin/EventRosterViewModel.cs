namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record EventRosterViewModel(EventRosterContext Event, EventRosterQuery Query, int Active, int Cancelled,
    IReadOnlyList<string> Colleges, IReadOnlyList<string> Programs, IReadOnlyList<int> Years,
    IReadOnlyList<RosterAttendee> Attendees, int Total, int TotalPages);
