using System.ComponentModel.DataAnnotations;

namespace IdentityTenantManagement.Models.Roles;

public class CreateRoleRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public List<Guid> PermissionIds { get; set; } = new();
}

public class AssignPermissionToRoleRequest
{
    [Required]
    public Guid RoleId { get; set; }

    [Required]
    public Guid PermissionId { get; set; }
}

public class PermissionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PermissionGroup { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class UpdateRoleRequest
{
    [Required]
    [StringLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
}

public class RoleWithPermissionsDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<PermissionDto> Permissions { get; set; } = new();
}