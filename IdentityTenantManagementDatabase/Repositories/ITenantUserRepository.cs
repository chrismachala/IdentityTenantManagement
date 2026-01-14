using IdentityTenantManagementDatabase.Models;

namespace IdentityTenantManagementDatabase.Repositories;

public interface ITenantUserRepository : IRepository<TenantUser>
{
    Task<IEnumerable<TenantUser>> GetByTenantIdAsync(Guid tenantId);
    Task<IEnumerable<TenantUser>> GetByUserIdAsync(Guid userId);
    Task<TenantUser?> GetByTenantAndUserIdAsync(Guid tenantId, Guid userId);
    Task<List<string>> GetUserPermissionsAsync(Guid tenantId, Guid userId);
    Task<int> CountUsersWithRoleInTenantAsync(Guid tenantId, Guid roleId);

    /// <summary>
    /// Gets users for a tenant with optional filtering by active/inactive status.
    /// Supports pagination for performance with large user lists.
    /// </summary>
    /// <param name="tenantId">The tenant ID to get users for</param>
    /// <param name="includeInactive">Whether to include inactive users (default: false)</param>
    /// <param name="page">Page number (1-based, default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 50)</param>
    /// <returns>Paginated list of users with their profile information</returns>
    Task<(IEnumerable<TenantUser> Users, int TotalCount)> GetUsersAsync(
        Guid tenantId,
        bool includeInactive = false,
        int page = 1,
        int pageSize = 50);
}