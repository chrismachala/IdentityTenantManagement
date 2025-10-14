using IdentityTenantManagementDatabase.Models;

namespace IdentityTenantManagement.Repositories;

public interface IIdentityProviderRepository : IRepository<IdentityProvider>
{
    Task<IdentityProvider?> GetByNameAsync(string name);
}