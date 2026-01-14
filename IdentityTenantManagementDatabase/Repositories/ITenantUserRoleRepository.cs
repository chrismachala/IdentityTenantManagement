using IdentityTenantManagementDatabase.Models;

namespace IdentityTenantManagementDatabase.Repositories;

public interface ITenantUserRoleRepository : IRepository<TenantUserRole>
{
    /// <summary>
    /// Gets all roles assigned to a specific tenant user
    /// </summary>
    Task<IEnumerable<TenantUserRole>> GetByTenantUserIdAsync(Guid tenantUserId);

    /// <summary>
    /// Gets a specific role assignment for a tenant user
    /// </summary>
    Task<TenantUserRole?> GetByTenantUserIdAndRoleIdAsync(Guid tenantUserId, Guid roleId);

    /// <summary>
    /// Checks if a tenant user has a specific role assigned
    /// </summary>
    Task<bool> HasRoleAsync(Guid tenantUserId, Guid roleId);

    /// <summary>
    /// Gets all tenant users that have a specific role
    /// </summary>
    Task<IEnumerable<TenantUserRole>> GetByRoleIdAsync(Guid roleId);
}