using IdentityTenantManagement.Constants;
using IdentityTenantManagementDatabase.Models;
using IdentityTenantManagementDatabase.Repositories;
using IO.Swagger.Model;
using KeycloakAdapter.Models;
using KeycloakAdapter.Services;

namespace IdentityTenantManagement.Services;

public interface IUserService
{
    Task UpdateUserAsync(string userId, EditUserModel model, string tenantId);
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

    public async Task UpdateUserAsync(string userId, EditUserModel model, string tenantId)
    {
        _logger.LogInformation("Updating user profile for {UserId} in tenant {TenantId}", userId, tenantId);

        try
        {
            var keycloakProviderId = Guid.Parse("049284C1-FF29-4F28-869F-F64300B69719");

            // Get user's internal ID from Keycloak external identity
            var userExternalIdentity = await _unitOfWork.ExternalIdentities.GetByExternalIdentifierAsync(userId, keycloakProviderId);
            if (userExternalIdentity == null)
            {
                throw new InvalidOperationException($"External identity not found for Keycloak user {userId}. User may not be synchronized to database.");
            }

            // Get tenant's internal ID from Keycloak external identity
            var tenantExternalIdentity = await _unitOfWork.ExternalIdentities.GetByExternalIdentifierAsync(tenantId, keycloakProviderId);
            if (tenantExternalIdentity == null)
            {
                throw new InvalidOperationException($"Tenant with Keycloak ID {tenantId} not found in database.");
            }

            // Get TenantUser relationship
            var tenantUser = await _unitOfWork.TenantUsers.GetByTenantAndUserIdAsync(tenantExternalIdentity.EntityId, userExternalIdentity.EntityId);
            if (tenantUser == null)
            {
                throw new InvalidOperationException($"User {userId} is not a member of tenant {tenantId}.");
            }

            // Begin database transaction
            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // Get or create TenantUserProfile
                var tenantUserProfile = await _unitOfWork.TenantUserProfiles.GetByTenantUserIdAsync(tenantUser.Id);

                if (tenantUserProfile == null)
                {
                    _logger.LogInformation("Creating new profile for TenantUser {TenantUserId}", tenantUser.Id);

                    // Create new UserProfile
                    var userProfile = new UserProfile
                    {
                        FirstName = model.FirstName,
                        LastName = model.LastName
                    };
                    await _unitOfWork.UserProfiles.AddAsync(userProfile);

                    // Create TenantUserProfile linking
                    tenantUserProfile = new TenantUserProfile
                    {
                        TenantUserId = tenantUser.Id,
                        UserProfileId = userProfile.Id
                    };
                    await _unitOfWork.TenantUserProfiles.AddAsync(tenantUserProfile);
                }
                else
                {
                    _logger.LogInformation("Updating existing profile {ProfileId} for TenantUser {TenantUserId}",
                        tenantUserProfile.UserProfileId, tenantUser.Id);

                    // Update existing UserProfile
                    var userProfile = await _unitOfWork.UserProfiles.GetByIdAsync(tenantUserProfile.UserProfileId);
                    if (userProfile == null)
                    {
                        throw new InvalidOperationException($"UserProfile {tenantUserProfile.UserProfileId} not found.");
                    }

                    userProfile.FirstName = model.FirstName;
                    userProfile.LastName = model.LastName;
                    userProfile.UpdatedAt = DateTime.UtcNow;

                    await _unitOfWork.UserProfiles.UpdateAsync(userProfile);
                }

                await _unitOfWork.CommitAsync();
                _logger.LogInformation("Successfully updated user profile for {UserId} in tenant {TenantId}", userId, tenantId);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                _logger.LogError("Failed to update user profile. Transaction rolled back.");
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile for {UserId} in tenant {TenantId}", userId, tenantId);
            throw;
        }
    }

    public async Task AddInvitedUserToDatabaseAsync(string keycloakUserId, string keycloakTenantId, string email, string firstName, string lastName)
    {
        _logger.LogInformation("Adding invited user {Email} to database with Keycloak ID {UserId} and Tenant ID {TenantId}",
            email, keycloakUserId, keycloakTenantId);

        try
        {
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

            // Get org-user role (default role for invited users) - validate before transaction
            var orgUserRole = await _roleService.GetRoleByNameAsync("org-user");
            if (orgUserRole == null)
            {
                throw new InvalidOperationException("org-user role not found in database. Ensure roles are pre-seeded.");
            }

            // Begin database transaction for atomic operation
            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // Generate internal GUID for user (NOT using Keycloak GUID)
                var userId = Guid.NewGuid();

                // Get the active status
                var activeStatus = await _unitOfWork.UserStatusTypes.GetByNameAsync("active");
                if (activeStatus == null)
                {
                    throw new InvalidOperationException("Active user status type not found in database. Ensure it is pre-seeded.");
                }

                // Create user with internal GUID (without name - that goes in profile)
                var user = new User
                {
                    Id = userId,
                    Email = email,
                    StatusId = activeStatus.Id
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

                // Create TenantUser relationship (many-to-many join) with org-user role
                var tenantUser = new TenantUser
                {
                    TenantId = tenantId,
                    UserId = userId,
                    TenantUserRoles = new List<TenantUserRole>
                    {
                        new TenantUserRole
                        {
                            RoleId = orgUserRole.Id
                        }
                    }
                };
                await _unitOfWork.TenantUsers.AddAsync(tenantUser);

                // Create UserProfile with the user's name
                var userProfile = new UserProfile
                {
                    FirstName = firstName,
                    LastName = lastName
                };
                await _unitOfWork.UserProfiles.AddAsync(userProfile);

                // Create TenantUserProfile linking the profile to this tenant-user relationship
                var tenantUserProfile = new TenantUserProfile
                {
                    TenantUserId = tenantUser.Id,
                    UserProfileId = userProfile.Id
                };
                await _unitOfWork.TenantUserProfiles.AddAsync(tenantUserProfile);

                // Commit all changes atomically
                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Successfully added invited user {Email} to database", email);
            }
            catch
            {
                // Rollback transaction on any failure
                await _unitOfWork.RollbackAsync();
                _logger.LogError("Failed to add invited user {Email} to database. Transaction rolled back.", email);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding invited user {Email} to database", email);
            throw;
        }
    }
}