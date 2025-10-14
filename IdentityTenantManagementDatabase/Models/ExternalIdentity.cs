namespace IdentityTenantManagementDatabase.Models;

public class ExternalIdentity
{
    public Guid Id { get; set; }

    public Guid ProviderId { get; set; }
    public IdentityProvider Provider { get; set; } = default!;

    // Polymorphic link (either a user or tenant)
    public string EntityType { get; set; } = default!; // "user" or "tenant"
    public Guid EntityId { get; set; }                 // maps to either User.Id or Tenant.Id

    public string ExternalIdentifier { get; set; } = default!; // Keycloak GUID, etc.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}