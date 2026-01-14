using System.ComponentModel.DataAnnotations;

namespace IdentityTenantManagement.Models.Roles;

public class AssignRoleRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid RoleId { get; set; }
}