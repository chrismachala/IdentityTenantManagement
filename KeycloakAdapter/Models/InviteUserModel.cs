using System.ComponentModel.DataAnnotations;

namespace KeycloakAdapter.Models;

public class InviteUserModel
{
    [Required]
    public string TenantId { get; set; }

    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}