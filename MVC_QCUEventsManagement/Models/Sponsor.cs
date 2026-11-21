namespace MVC_QCUEventsManagement.Models;

public sealed class Sponsor
{
    public int SponsorEntryId { get; set; }
    public required string SponsorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
}
