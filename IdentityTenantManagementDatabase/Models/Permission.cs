namespace IdentityTenantManagementDatabase.Models;

public class Permission
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!; // invite-users, view-users, update-users, delete-users, assign-permissions, update-org-settings
    public string DisplayName { get; set; } = default!;
    public string Description { get; set; } = default!;
    public Guid PermissionGroupId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PermissionGroup PermissionGroup { get; set; } = default!;
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}