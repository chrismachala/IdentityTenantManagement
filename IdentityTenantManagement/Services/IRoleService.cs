using IdentityTenantManagementDatabase.Models;

namespace IdentityTenantManagement.Services;

public interface IRoleService
{
    Task<List<Role>> GetAllRolesAsync();
    Task<Role?> GetRoleByIdAsync(Guid id);
    Task<Role?> GetRoleByNameAsync(string name);
    Task<Guid> GetDefaultUserRoleIdAsync();
}