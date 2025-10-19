using IdentityTenantManagementDatabase.Repositories;

namespace IdentityTenantManagement.Services;

public class PermissionService
{
    private readonly ITenantUserRepository _tenantUserRepository;

    public PermissionService(ITenantUserRepository tenantUserRepository)
    {
        _tenantUserRepository = tenantUserRepository;
    }

    public async Task<List<string>> GetUserPermissionsAsync(Guid tenantId, Guid userId)
    {
        return await _tenantUserRepository.GetUserPermissionsAsync(tenantId, userId);
    }

    public async Task<bool> UserHasPermissionAsync(Guid tenantId, Guid userId, string permissionName)
    {
        var permissions = await GetUserPermissionsAsync(tenantId, userId);
        return permissions.Contains(permissionName);
    }

    public async Task<bool> UserHasAnyPermissionAsync(Guid tenantId, Guid userId, params string[] permissionNames)
    {
        var permissions = await GetUserPermissionsAsync(tenantId, userId);
        return permissionNames.Any(p => permissions.Contains(p));
    }

    public async Task<bool> UserHasAllPermissionsAsync(Guid tenantId, Guid userId, params string[] permissionNames)
    {
        var permissions = await GetUserPermissionsAsync(tenantId, userId);
        return permissionNames.All(p => permissions.Contains(p));
    }
}