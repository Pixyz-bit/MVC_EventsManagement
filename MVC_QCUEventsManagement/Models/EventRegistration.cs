namespace MVC_QCUEventsManagement.Models;

public sealed class EventRegistration
{
    public int EventRegistrationId { get; set; }
    public int EventId { get; set; }
    public required string StudentId { get; set; }
    public int CurrentYearLvl { get; set; }
    public required string CurrentSection { get; set; }
    public string Status { get; set; } = "NoShow";
    public DateTime? CheckInTimestamp { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Event Event { get; set; } = null!;
    public Student Student { get; set; } = null!;
}
