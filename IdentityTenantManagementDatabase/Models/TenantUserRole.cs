namespace IdentityTenantManagementDatabase.Models;

public class TenantUserRole
{
    public Guid Id { get; set; }

    public Guid TenantUserId { get; set; }
    public TenantUser TenantUser { get; set; } = default!;

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = default!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}