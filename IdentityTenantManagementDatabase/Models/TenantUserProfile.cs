namespace IdentityTenantManagementDatabase.Models;

public class TenantUserProfile
{
    public Guid Id { get; set; }
    public Guid TenantUserId { get; set; }
    public TenantUser TenantUser { get; set; } = default!;

    public Guid UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}