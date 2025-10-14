namespace IdentityTenantManagementDatabase.Models;

public class User
{
    public Guid Id { get; set; }
    //
    // public Guid TenantId { get; set; }
    // public Tenant Tenant { get; set; } = default!;

    public string Email { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Role { get; set; } = "member";
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ExternalIdentity> ExternalIdentities { get; set; } = new List<ExternalIdentity>();
}