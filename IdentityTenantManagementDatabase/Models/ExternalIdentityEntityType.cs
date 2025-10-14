namespace IdentityTenantManagementDatabase.Models;

public class ExternalIdentityEntityType
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = default!;

    // Navigation
    public ICollection<ExternalIdentity> ExternalIdentities { get; set; } = new List<ExternalIdentity>();
}