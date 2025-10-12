using System.ComponentModel.DataAnnotations;

namespace IdentityTenantManagement.Models.Organisations;

public class InviteUserModel
{
    [Required]
    public string TenantId { get; set; }

    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}