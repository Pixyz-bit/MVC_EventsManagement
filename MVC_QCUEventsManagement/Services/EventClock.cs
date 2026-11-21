namespace MVC_QCUEventsManagement.Services;

/// <summary>Provides Philippine wall-clock values for the application's SQL datetime fields.</summary>
public sealed class EventClock(TimeProvider timeProvider)
{
    private static readonly TimeZoneInfo ApplicationTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

    public DateTime Now => TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), ApplicationTimeZone).DateTime;
}
