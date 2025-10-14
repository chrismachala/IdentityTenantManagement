using IdentityTenantManagementDatabase.Models;

namespace IdentityTenantManagement.Repositories;

public interface IExternalIdentityRepository : IRepository<ExternalIdentity>
{
    Task<ExternalIdentity?> GetByExternalIdentifierAsync(string externalIdentifier, Guid providerId);
    Task<IEnumerable<ExternalIdentity>> GetByEntityAsync(Guid entityTypeId, Guid entityId);
}