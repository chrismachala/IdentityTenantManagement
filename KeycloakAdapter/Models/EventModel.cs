namespace KeycloakAdapter.Models;

public class EventModel
{
    public string Type { get; set; } = string.Empty;
    public string RealmId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public long Time { get; set; }
    public Dictionary<string, string>? Details { get; set; }
}

public class RegistrationEventResult
{
    public string UserId { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}