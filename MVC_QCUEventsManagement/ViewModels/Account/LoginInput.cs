using System.ComponentModel.DataAnnotations;

namespace MVC_QCUEventsManagement.ViewModels.Account;

public sealed class LoginInput
{
    [Required(ErrorMessage = "Enter your student ID or email.")]
    [StringLength(150)]
    public string Identifier { get; set; } = "";

    [Required(ErrorMessage = "Enter your password.")]
    [StringLength(128)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}
