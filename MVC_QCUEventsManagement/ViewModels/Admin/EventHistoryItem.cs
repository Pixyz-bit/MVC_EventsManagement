namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record EventHistoryItem(int EventId, string Title, string? College, string Venue, DateTime StartsAt, string Outcome);
