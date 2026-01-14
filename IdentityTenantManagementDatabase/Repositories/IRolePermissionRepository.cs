using IdentityTenantManagementDatabase.Models;

namespace IdentityTenantManagementDatabase.Repositories;

public interface IRolePermissionRepository : IRepository<RolePermission>
{
    /// <summary>
    /// Gets all permissions assigned to a specific role
    /// </summary>
    Task<IEnumerable<RolePermission>> GetByRoleIdAsync(Guid roleId);

    /// <summary>
    /// Gets a specific permission assignment for a role
    /// </summary>
    Task<RolePermission?> GetByRoleIdAndPermissionIdAsync(Guid roleId, Guid permissionId);

    /// <summary>
    /// Checks if a role has a specific permission assigned
    /// </summary>
    Task<bool> HasPermissionAsync(Guid roleId, Guid permissionId);

    /// <summary>
    /// Gets all roles that have a specific permission
    /// </summary>
    Task<IEnumerable<RolePermission>> GetByPermissionIdAsync(Guid permissionId);
}