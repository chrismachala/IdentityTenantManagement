using IdentityTenantManagementDatabase.Models;

namespace IdentityTenantManagementDatabase.Repositories;

public interface IUserStatusTypeRepository : IRepository<UserStatusType>
{
    Task<UserStatusType?> GetByNameAsync(string name);
}