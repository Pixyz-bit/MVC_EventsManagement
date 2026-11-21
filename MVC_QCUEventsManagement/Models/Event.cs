namespace MVC_QCUEventsManagement.Models;

public sealed class Event
{
    public int EventId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required string VenueLocation { get; set; }
    public int MaxCapacity { get; set; }
    public int CurrentRegistrations { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime EventStart { get; set; }
    public DateTime EventEnd { get; set; }
    public DateTime RegStart { get; set; }
    public DateTime RegEnd { get; set; }
    public string Status { get; set; } = "Upcoming";
    public string? CancellationReason { get; set; }
    public string? TargetBranch { get; set; }
    public string? TargetDepartment { get; set; }
    public string? TargetProgram { get; set; }
    public int? TargetYearLevel { get; set; }
    public string? EventPhotoPath { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public User CreatedByUser { get; set; } = null!;
    public ICollection<Sponsor> Sponsors { get; set; } = new List<Sponsor>();
    public ICollection<EventRegistration> Registrations { get; set; } = new List<EventRegistration>();
}
