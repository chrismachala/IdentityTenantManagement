using System.ComponentModel.DataAnnotations;

namespace KeycloakAdapter.Models;

public class LoginModel
{
    [Required(ErrorMessage = "Organization is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Organization must be between 2 and 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9-_]+$", ErrorMessage = "Organization can only contain letters, numbers, hyphens, and underscores")]
    public string Organization { get; set; } = string.Empty;

    [Required(ErrorMessage = "Username is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Username must be between 2 and 100 characters")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}