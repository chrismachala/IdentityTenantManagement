namespace IdentityTenantManagementDatabase.Models;

public class IdentityProvider
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;        // e.g. "Keycloak"
    public string ProviderType { get; set; } = default!; // e.g. "oidc", "saml"
    public string? BaseUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ExternalIdentity> ExternalIdentities { get; set; } = new List<ExternalIdentity>();
}