namespace IdentityTenantManagementDatabase.Models;

public class UserProfile
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<TenantUserProfile> TenantUserProfiles { get; set; } = new List<TenantUserProfile>();
}