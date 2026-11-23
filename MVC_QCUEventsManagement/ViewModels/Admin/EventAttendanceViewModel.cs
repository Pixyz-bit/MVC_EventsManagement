namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record EventAttendanceViewModel(EventRosterContext Event, EventAttendanceQuery Query,
    int CheckedIn, IReadOnlyList<string> Colleges, IReadOnlyList<string> Programs,
    IReadOnlyList<AttendanceItem> Attendees, int Total, int TotalPages);
