namespace IdentityTenantManagementDatabase.Models;

public class RegistrationFailureLog
{
    public Guid Id { get; set; }
    public string KeycloakUserId { get; set; } = string.Empty;
    public string KeycloakTenantId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorDetails { get; set; } = string.Empty;
    public bool KeycloakUserRolledBack { get; set; } = false;
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;
}