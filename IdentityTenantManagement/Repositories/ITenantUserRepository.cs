using IdentityTenantManagementDatabase.Models;

namespace IdentityTenantManagement.Repositories;

public interface ITenantUserRepository : IRepository<TenantUser>
{
    Task<IEnumerable<TenantUser>> GetByTenantIdAsync(Guid tenantId);
    Task<IEnumerable<TenantUser>> GetByUserIdAsync(Guid userId);
}