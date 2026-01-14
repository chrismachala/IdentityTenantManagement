namespace IdentityTenantManagementDatabase.Models;

public class DeletionPolicyConfiguration
{
    public Guid Id { get; set; }
    public bool PermanentDeletionEnabled { get; set; } = false;
    public int RetentionPeriodDays { get; set; } = 90;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = default!;
}