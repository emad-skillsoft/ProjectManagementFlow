using System.ComponentModel.DataAnnotations;

namespace ProjectManagmentFlow.ViewModels;

public class AccountLoginViewModel
{
    [Required(ErrorMessage = "Login_EmailRequired")]
    [EmailAddress(ErrorMessage = "Login_EmailInvalid")]
    [Display(Name = "Login_Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Login_PasswordRequired")]
    [DataType(DataType.Password)]
    [Display(Name = "Login_Password")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
