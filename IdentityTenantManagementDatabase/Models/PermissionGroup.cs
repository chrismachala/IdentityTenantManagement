namespace IdentityTenantManagementDatabase.Models;

public class PermissionGroup
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!; // SystemAdministration, Reporting, Billing, etc.
    public string DisplayName { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
}