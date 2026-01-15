using System.ComponentModel.DataAnnotations;

namespace KeycloakAdapter.Models;

public class OrganizationSelectionModel
{
    [Required(ErrorMessage = "Access token is required")]
    public string AccessToken { get; set; } = string.Empty;

    [Required(ErrorMessage = "Organization ID is required")]
    public string OrganizationId { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "ExpiresIn must be a positive number")]
    public int ExpiresIn { get; set; }
}
