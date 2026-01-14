namespace IdentityTenantManagementDatabase.Repositories;

using IdentityTenantManagementDatabase.Models;

public interface IGlobalSettingsRepository : IRepository<GlobalSettings>
{
    Task<GlobalSettings?> GetByKeyAsync(string key);
    Task<IEnumerable<GlobalSettings>> GetAllSettingsAsync();
    Task UpsertAsync(string key, string value, string? description = null);
}