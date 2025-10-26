using IdentityTenantManagement.Constants;
using IdentityTenantManagementDatabase.Models;
using IdentityTenantManagementDatabase.Repositories;
using KeycloakAdapter.Models;
using KeycloakAdapter.Services;

namespace IdentityTenantManagement.Services;

public interface IUserService
{
    Task UpdateUserAsync(string userId, CreateUserModel model);
    Task AddInvitedUserToDatabaseAsync(string keycloakUserId, string keycloakTenantId, string email, string firstName, string lastName);
}

public class UserService : IUserService
{
    private readonly IKCUserService _kcUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserService> _logger;
    private readonly IRoleService _roleService;

    public UserService(
        IKCUserService kcUserService,
        IUnitOfWork unitOfWork,
        ILogger<UserService> logger,
        IRoleService roleService)
    {
        _kcUserService = kcUserService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _roleService = roleService;
    }

    public async Task UpdateUserAsync(string userId, CreateUserModel model)
    {
        _logger.LogInformation("Updating user {UserId} in Keycloak and database", userId);

        // Update in Keycloak first
        await _kcUserService.UpdateUserAsync(userId, model);

        // Then update in database
        // Hardcoded Keycloak provider ID (matches the seeded provider in DbContext)
        var keycloakProviderId = Guid.Parse("049284C1-FF29-4F28-869F-F64300B69719");
        var userExternalIdentity = await _unitOfWork.ExternalIdentities.GetByExternalIdentifierAsync(userId, keycloakProviderId);

        if (userExternalIdentity != null)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userExternalIdentity.EntityId);

            if (user != null)
            {
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.Email = model.Email;

                await _unitOfWork.Users.UpdateAsync(user);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Successfully updated user {UserId} in database", userId);
            }
            else
            {
                _logger.LogWarning("User not found in database for external ID {UserId}", userId);
            }
        }
        else
        {
            _logger.LogWarning("External identity not found for user {UserId}", userId);
        }
    }

    public async Task AddInvitedUserToDatabaseAsync(string keycloakUserId, string keycloakTenantId, string email, string firstName, string lastName)
    {
        _logger.LogInformation("Adding invited user {Email} to database with Keycloak ID {UserId} and Tenant ID {TenantId}",
            email, keycloakUserId, keycloakTenantId);

        // Look up the pre-seeded Keycloak identity provider
        var keycloakProvider = await _unitOfWork.IdentityProviders.GetByNameAsync("Keycloak");
        if (keycloakProvider == null)
        {
            throw new InvalidOperationException("Keycloak identity provider not found in database. Ensure it is pre-seeded.");
        }

        // Check if user already exists in database (by external identity)
        var existingUserExternalIdentity = await _unitOfWork.ExternalIdentities.GetByExternalIdentifierAsync(keycloakUserId, keycloakProvider.Id);
        if (existingUserExternalIdentity != null)
        {
            _logger.LogWarning("User with Keycloak ID {UserId} already exists in database. Skipping creation.", keycloakUserId);
            return;
        }

        // Check if tenant exists in database (by external identity)
        var tenantExternalIdentity = await _unitOfWork.ExternalIdentities.GetByExternalIdentifierAsync(keycloakTenantId, keycloakProvider.Id);
        if (tenantExternalIdentity == null)
        {
            throw new InvalidOperationException($"Tenant with Keycloak ID {keycloakTenantId} not found in database. Ensure the tenant exists before inviting users.");
        }

        var tenantId = tenantExternalIdentity.EntityId;

        // Generate internal GUID for user (NOT using Keycloak GUID)
        var userId = Guid.NewGuid();

        // Create user with internal GUID
        var user = new User
        {
            Id = userId,
            Email = email,
            FirstName = firstName,
            LastName = lastName
        };
        await _unitOfWork.Users.AddAsync(user);

        // Create ExternalIdentity for user linking internal ID to Keycloak GUID
        var userExternalIdentity = new ExternalIdentity
        {
            ProviderId = keycloakProvider.Id,
            EntityTypeId = ExternalIdentityEntityTypeIds.User,
            EntityId = userId,
            ExternalIdentifier = keycloakUserId
        };
        await _unitOfWork.ExternalIdentities.AddAsync(userExternalIdentity);

        // Get org-user role (default role for invited users)
        var orgUserRole = await _roleService.GetRoleByNameAsync("org-user");
        if (orgUserRole == null)
        {
            throw new InvalidOperationException("org-user role not found in database. Ensure roles are pre-seeded.");
        }

        // Create TenantUser relationship (many-to-many join) with org-user role
        var tenantUser = new TenantUser
        {
            TenantId = tenantId,
            UserId = userId,
            RoleId = orgUserRole.Id
        };
        await _unitOfWork.TenantUsers.AddAsync(tenantUser);

        // Save all changes to database
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Successfully added invited user {Email} to database", email);
    }
}