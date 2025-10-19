namespace IdentityTenantManagementDatabase.Models;

public class UserPermission
{
    public Guid Id { get; set; }
    public Guid TenantUserId { get; set; }
    public Guid PermissionId { get; set; }
    public Guid? GrantedByUserId { get; set; } // Who granted this permission
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public TenantUser TenantUser { get; set; } = default!;
    public Permission Permission { get; set; } = default!;
    public User? GrantedByUser { get; set; }
}