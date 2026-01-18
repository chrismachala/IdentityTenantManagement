namespace IdentityTenantManagementDatabase.Repositories;

using Models;

public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task LogAsync(
        string action,
        string resourceType,
        string resourceId,
        Guid? actorUserId = null,
        Guid? tenantId = null,
        string? oldValues = null,
        string? newValues = null,
        string? ipAddress = null,
        string? userAgent = null);

    Task AnonymizeLogsForUserAsync(Guid userId);

    Task<List<AuditLog>> GetByTenantIdAsync(Guid tenantId, int page = 1, int pageSize = 50);

    Task<int> GetCountByTenantIdAsync(Guid tenantId);
}
