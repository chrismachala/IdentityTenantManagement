using IdentityTenantManagementDatabase.Models;

namespace IdentityTenantManagementDatabase.Repositories;

public interface ITenantUserRepository : IRepository<TenantUser>
{
    Task<IEnumerable<TenantUser>> GetByTenantIdAsync(Guid tenantId);
    Task<IEnumerable<TenantUser>> GetByUserIdAsync(Guid userId);
    Task<TenantUser?> GetByTenantAndUserIdAsync(Guid tenantId, Guid userId);
    Task<List<string>> GetUserPermissionsAsync(Guid tenantId, Guid userId);
}