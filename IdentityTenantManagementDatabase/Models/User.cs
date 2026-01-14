namespace IdentityTenantManagementDatabase.Models;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Global inactive state tracking
    public DateTime? GloballyInactiveAt { get; set; }

    // Permanent deletion failure tracking
    public DateTime? DeletionFailedAt { get; set; }
    public string? DeletionFailedReason { get; set; }
    public int DeletionRetryCount { get; set; } = 0;

    // Navigation
    public ICollection<ExternalIdentity> ExternalIdentities { get; set; } = new List<ExternalIdentity>();
    public ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
}