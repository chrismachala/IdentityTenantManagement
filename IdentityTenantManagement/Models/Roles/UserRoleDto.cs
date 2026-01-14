namespace IdentityTenantManagement.Models.Roles;

public class UserRoleDto
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = default!;
    public string RoleDisplayName { get; set; } = default!;
    public string RoleDescription { get; set; } = default!;
    public DateTime AssignedAt { get; set; }
}

public class UserRolesResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public List<UserRoleDto> Roles { get; set; } = new();
    public List<PermissionDto> DirectPermissions { get; set; } = new();
}

public class RoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string Description { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}