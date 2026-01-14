namespace IdentityTenantManagementDatabase.Repositories;

using Models;

public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task LogAsync(
        string action,
        string resourceType,
        string resourceId,
        Guid? actorUserId = null,
        string? actorDisplayName = null,
        Guid? tenantId = null,
        string? oldValues = null,
        string? newValues = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? additionalContext = null);

    Task AnonymizeLogsForUserAsync(Guid userId);
}
