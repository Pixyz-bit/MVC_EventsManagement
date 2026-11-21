namespace MVC_QCUEventsManagement.Models;

public sealed class User
{
    public int UserId { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string PasswordSalt { get; set; }
    public required string Role { get; set; }
    public bool IsActive { get; set; } = true;

    public Student? Student { get; set; }
    public ICollection<Event> CreatedEvents { get; set; } = new List<Event>();
}
