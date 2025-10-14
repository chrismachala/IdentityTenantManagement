 

using IdentityTenantManagementDatabase.Models;

namespace IdentityTenantManagement.Repositories;

public interface ITenantRepository : IRepository<Tenant>
{
    Task<Tenant?> GetByDomainAsync(string domain);
    Task<Tenant?> GetByNameAsync(string name);
}