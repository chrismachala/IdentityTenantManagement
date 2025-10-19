namespace IdentityTenantManagementDatabase.Models;

public class Role
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!; // org-admin, org-manager, org-user
    public string DisplayName { get; set; } = default!;
    public string Description { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
}