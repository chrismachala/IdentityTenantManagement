using IdentityTenantManagementDatabase.Models;

namespace IdentityTenantManagement.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<IEnumerable<User>> GetByTenantIdAsync(Guid tenantId);
}