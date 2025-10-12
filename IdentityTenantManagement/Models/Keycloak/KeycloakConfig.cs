namespace IdentityTenantManagement.Models.Keycloak;

public class KeycloakConfig
{
    public string BaseUrl { get; set; }
    public string Realm { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string AdminUsername { get; set; }
    public string AdminPassword { get; set; }
}
