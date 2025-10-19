namespace IdentityTenantManagementDatabase.Models;

public class RolePermission
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = default!;

    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}