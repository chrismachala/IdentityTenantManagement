using IdentityTenantManagementDatabase.Models;

namespace IdentityTenantManagementDatabase.Repositories;

public interface IRoleRepository : IRepository<Role>
{
    Task<Role?> GetByNameAsync(string name);
    Task<List<Role>> GetAllWithPermissionsAsync();
    Task<Role?> GetByIdWithPermissionsAsync(Guid id);
}