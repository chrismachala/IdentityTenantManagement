namespace IdentityTenantManagementDatabase.Models;

public class TenantUser
{

    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = default!;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}