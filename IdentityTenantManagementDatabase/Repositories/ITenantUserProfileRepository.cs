using IdentityTenantManagementDatabase.Models;

namespace IdentityTenantManagementDatabase.Repositories;

public interface ITenantUserProfileRepository : IRepository<TenantUserProfile>
{
    Task<TenantUserProfile?> GetByTenantUserIdAsync(Guid tenantUserId);
}