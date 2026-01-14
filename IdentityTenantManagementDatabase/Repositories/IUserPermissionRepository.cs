using IdentityTenantManagementDatabase.Models;

namespace IdentityTenantManagementDatabase.Repositories;

public interface IUserPermissionRepository : IRepository<UserPermission>
{
    Task<List<UserPermission>> GetByTenantUserIdAsync(Guid tenantUserId);
    Task<UserPermission?> GetByTenantUserIdAndPermissionIdAsync(Guid tenantUserId, Guid permissionId);
    Task<bool> HasPermissionAsync(Guid tenantUserId, Guid permissionId);
}
