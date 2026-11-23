using System.ComponentModel.DataAnnotations;

namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed class CheckInInput
{
    [Range(1, int.MaxValue)] public int RegistrationId { get; set; }
    [Required, StringLength(12)] public string Version { get; set; } = "";
    [Required, StringLength(12)] public string EventVersion { get; set; } = "";
    public bool Confirmed { get; set; }
}
