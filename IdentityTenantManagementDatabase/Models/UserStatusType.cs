namespace IdentityTenantManagementDatabase.Models;

public class UserStatusType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!; // e.g., "active", "inactive", "suspended", "pending"
    public string DisplayName { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<User> Users { get; set; } = new List<User>();
}