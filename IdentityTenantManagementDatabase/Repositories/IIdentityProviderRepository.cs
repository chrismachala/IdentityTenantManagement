using IdentityTenantManagementDatabase.Models;

namespace IdentityTenantManagementDatabase.Repositories;

public interface IIdentityProviderRepository : IRepository<IdentityProvider>
{
    Task<IdentityProvider?> GetByNameAsync(string name);
}