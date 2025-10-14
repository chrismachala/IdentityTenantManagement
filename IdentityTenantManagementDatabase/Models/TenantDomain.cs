namespace IdentityTenantManagementDatabase.Models;

public class TenantDomain
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;

    public string Domain { get; set; } = default!;

    public bool IsPrimary { get; set; } = false;
    public bool IsVerified { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}