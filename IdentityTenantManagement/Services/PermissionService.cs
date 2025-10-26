using IdentityTenantManagementDatabase.Repositories;

namespace IdentityTenantManagement.Services;

public class PermissionService
{
    private readonly IUnitOfWork _unitOfWork;

    public PermissionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<string>> GetUserPermissionsAsync(Guid tenantId, Guid userId)
    {
        return await _unitOfWork.TenantUsers.GetUserPermissionsAsync(tenantId, userId);
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

    public async Task<List<string>> GetUserPermissionsByExternalIdsAsync(string keycloakUserId, string keycloakOrgId)
    {
        // Hardcoded Keycloak provider ID (matches the seeded provider in DbContext)
        var keycloakProviderId = Guid.Parse("049284C1-FF29-4F28-869F-F64300B69719");

        // Get user and tenant from database via external identities
        var userExternalIdentity = await _unitOfWork.ExternalIdentities.GetByExternalIdentifierAsync(keycloakUserId, keycloakProviderId);
        var tenantExternalIdentity = await _unitOfWork.ExternalIdentities.GetByExternalIdentifierAsync(keycloakOrgId, keycloakProviderId);

        if (userExternalIdentity == null || tenantExternalIdentity == null)
        {
            return new List<string>();
        }

        return await GetUserPermissionsAsync(tenantExternalIdentity.EntityId, userExternalIdentity.EntityId);
    }
}