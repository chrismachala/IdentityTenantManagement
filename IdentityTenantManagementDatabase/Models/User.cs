namespace IdentityTenantManagementDatabase.Models;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ExternalIdentity> ExternalIdentities { get; set; } = new List<ExternalIdentity>();
}