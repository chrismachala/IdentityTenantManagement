using IdentityTenantManagementDatabase.Models;

namespace IdentityTenantManagementDatabase.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<IEnumerable<User>> GetByTenantIdAsync(Guid tenantId);

    // Soft-delete methods
    Task<IEnumerable<Guid>> GetGloballyInactiveUserIdsAsync();
    Task MarkAsGloballyInactiveAsync(Guid userId);
    Task ClearGloballyInactiveStatusAsync(Guid userId);

    // Permanent deletion methods
    Task<IEnumerable<User>> GetDeletionFailedUsersAsync();
    Task MarkDeletionFailedAsync(Guid userId, string reason, int retryCount);
    Task ResetDeletionRetryAsync(Guid userId);
}