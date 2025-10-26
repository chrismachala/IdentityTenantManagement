using IdentityTenantManagementDatabase.Models;

namespace IdentityTenantManagementDatabase.Repositories;

public interface IRegistrationFailureLogRepository : IRepository<RegistrationFailureLog>
{
    Task<IEnumerable<RegistrationFailureLog>> GetByKeycloakUserIdAsync(string keycloakUserId);
    Task<IEnumerable<RegistrationFailureLog>> GetByKeycloakTenantIdAsync(string keycloakTenantId);
}