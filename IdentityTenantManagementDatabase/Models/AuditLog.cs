namespace IdentityTenantManagementDatabase.Models;

public class AuditLog
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Actor (who performed the action)
    public Guid? ActorUserId { get; set; }
    public User? ActorUser { get; set; }

    // Tenant context
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    // Action details
    public string Action { get; set; } = default!;
    public string ResourceType { get; set; } = default!;
    public string ResourceId { get; set; } = default!;

    // Change tracking (JSON serialized)
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    // Request metadata
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}