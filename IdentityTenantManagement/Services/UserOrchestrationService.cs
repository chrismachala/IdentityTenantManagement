using IdentityTenantManagement.Constants;
using IdentityTenantManagementDatabase.Models;
using IdentityTenantManagementDatabase.Repositories;
using IO.Swagger.Model;
using KeycloakAdapter.Models;
using KeycloakAdapter.Services;

namespace IdentityTenantManagement.Services;

public interface IUserOrchestrationService
{
    Task<string> CreateUserAsync(CreateUserModel model, string? organizationId = null);
    Task DeleteUserAsync(string keycloakUserId, string callingUserId, string tenantId);
}

public class UserOrchestrationService : IUserOrchestrationService
{
    private readonly IKCUserService _kcUserService;
    private readonly IKCOrganisationService _kcOrganisationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserOrchestrationService> _logger;
    private readonly IRoleService _roleService;

    public UserOrchestrationService(
        IKCUserService kcUserService,
        IKCOrganisationService kcOrganisationService,
        IUnitOfWork unitOfWork,
        ILogger<UserOrchestrationService> logger,
        IRoleService roleService)
    {
        _kcUserService = kcUserService;
        _kcOrganisationService = kcOrganisationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _roleService = roleService;
    }

    public async Task<string> CreateUserAsync(CreateUserModel model, string? organizationId = null)
    {
        _logger.LogInformation("Starting user creation saga for {Email}", model.Email);

        // Track saga state for compensating transactions
        string? createdUserId = null;
        bool addedToOrganization = false;
        bool databaseTransactionStarted = false;

        try
        {
            // Step 1: Create user in Keycloak
            _logger.LogInformation("Saga Step 1: Creating user in Keycloak");
            await _kcUserService.CreateUserAsync(model);
            var userRepresentation = await _kcUserService.GetUserByEmailAsync(model.Email);
            createdUserId = userRepresentation.Id;
            _logger.LogInformation("Saga Step 1: User created successfully with ID {UserId}", createdUserId);

            // Step 2: Add user to organization if specified
            if (!string.IsNullOrEmpty(organizationId))
            {
                _logger.LogInformation("Saga Step 2: Adding user {UserId} to organization {OrgId}", createdUserId, organizationId);
                await _kcOrganisationService.AddUserToOrganisationAsync(new UserTenantModel
                {
                    UserId = createdUserId,
                    TenantId = organizationId
                });
                addedToOrganization = true;
                _logger.LogInformation("Saga Step 2: User added to organization successfully");
            }

            // Step 3: Persist to database with transaction
            _logger.LogInformation("Saga Step 3: Persisting user to database");
            await _unitOfWork.BeginTransactionAsync();
            databaseTransactionStarted = true;

            await PersistUserToDatabaseAsync(userRepresentation, organizationId);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Saga completed successfully for user {Email} with ID {UserId}", model.Email, createdUserId);
            return createdUserId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga failed for user {Email}. Starting compensating transactions. State: UserId={UserId}, AddedToOrg={AddedToOrg}, DbTransaction={DbTransaction}",
                model.Email, createdUserId, addedToOrganization, databaseTransactionStarted);

            // Execute compensating transactions in reverse order
            await ExecuteCreateCompensatingTransactionsAsync(createdUserId, organizationId, addedToOrganization, databaseTransactionStarted);

            throw;
        }
    }

    public async Task DeleteUserAsync(string keycloakUserId, string callingUserId, string tenantId)
    {
        _logger.LogInformation("Starting user deletion saga for {UserId}. Called by {CallingUserId} in tenant {TenantId}",
            keycloakUserId, callingUserId, tenantId);

        // Security Check 1: Prevent self-deletion
        if (keycloakUserId == callingUserId)
        {
            _logger.LogWarning("User {UserId} attempted to delete themselves", callingUserId);
            throw new InvalidOperationException("You cannot delete your own account. Please contact another administrator.");
        }

        // Track saga state for compensating transactions
        UserRepresentation? deletedKeycloakUser = null;
        User? deletedDatabaseUser = null;
        ExternalIdentity? deletedExternalIdentity = null;
        List<TenantUser>? deletedTenantUsers = null;
        bool keycloakUserDeleted = false;
        bool databaseTransactionStarted = false;

        try
        {
            // Step 0: Security validations
            _logger.LogInformation("Saga Step 0: Performing security validations");
            var keycloakProviderId = Guid.Parse("049284C1-FF29-4F28-869F-F64300B69719");

            // Convert tenant ID to internal GUID
            var tenantExternalIdentity = await _unitOfWork.ExternalIdentities.GetByExternalIdentifierAsync(tenantId, keycloakProviderId);
            if (tenantExternalIdentity == null)
            {
                _logger.LogWarning("Tenant {TenantId} not found in database", tenantId);
                throw new InvalidOperationException("Tenant not found");
            }
            var internalTenantId = tenantExternalIdentity.EntityId;

            // Security Check 2: Verify user belongs to the calling user's tenant
            var userToDeleteExternalIdentity = await _unitOfWork.ExternalIdentities.GetByExternalIdentifierAsync(keycloakUserId, keycloakProviderId);
            if (userToDeleteExternalIdentity == null)
            {
                _logger.LogWarning("User {UserId} not found in database", keycloakUserId);
                throw new InvalidOperationException("User not found");
            }
            var internalUserToDeleteId = userToDeleteExternalIdentity.EntityId;

            var tenantUser = await _unitOfWork.TenantUsers.GetByTenantAndUserIdAsync(internalTenantId, internalUserToDeleteId);
            if (tenantUser == null)
            {
                _logger.LogWarning("User {UserId} attempted to delete user {TargetUserId} who does not belong to their tenant {TenantId}",
                    callingUserId, keycloakUserId, tenantId);
                throw new UnauthorizedAccessException("You can only delete users within your organization");
            }

            // Security Check 3: Prevent deletion of the last admin
            var orgAdminRole = await _roleService.GetRoleByNameAsync("org-admin");
            if (orgAdminRole != null && tenantUser.TenantUserRoles.Any(tur => tur.RoleId == orgAdminRole.Id))
            {
                var adminCount = await _unitOfWork.TenantUsers.CountUsersWithRoleInTenantAsync(internalTenantId, orgAdminRole.Id);
                if (adminCount <= 1)
                {
                    _logger.LogWarning("Attempted to delete the last admin {UserId} in tenant {TenantId}", keycloakUserId, tenantId);
                    throw new InvalidOperationException("Cannot delete the last administrator in the organization. Assign another administrator first.");
                }
            }

            _logger.LogInformation("Saga Step 0: Security validations passed");

            // Step 1: Capture user state for potential rollback
            _logger.LogInformation("Saga Step 1: Capturing user state for {UserId}", keycloakUserId);
            deletedKeycloakUser = await _kcUserService.GetUserByIdAsync(keycloakUserId);
            _logger.LogInformation("Saga Step 1: User state captured");

            // Step 2: Delete user from Keycloak
            _logger.LogInformation("Saga Step 2: Deleting user {UserId} from Keycloak", keycloakUserId);
            await _kcUserService.DeleteUserAsync(keycloakUserId);
            keycloakUserDeleted = true;
            _logger.LogInformation("Saga Step 2: User deleted from Keycloak successfully");

            // Step 3: Delete user from database with transaction
            _logger.LogInformation("Saga Step 3: Deleting user from database");
            var userExternalIdentity = await _unitOfWork.ExternalIdentities.GetByExternalIdentifierAsync(keycloakUserId, keycloakProviderId);

            if (userExternalIdentity != null)
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userExternalIdentity.EntityId);
                if (user != null)
                {
                    await _unitOfWork.BeginTransactionAsync();
                    databaseTransactionStarted = true;

                    // Store for potential rollback (though unlikely needed)
                    deletedDatabaseUser = user;
                    deletedExternalIdentity = userExternalIdentity;

                    // Delete TenantUser relationships
                    // Note: This assumes TenantUsers have UserId foreign key
                    // Adjust based on your actual schema
                    await _unitOfWork.Users.DeleteAsync(user.Id);
                    await _unitOfWork.ExternalIdentities.DeleteAsync(userExternalIdentity.Id);

                    await _unitOfWork.CommitAsync();
                    _logger.LogInformation("Saga Step 3: User deleted from database successfully");
                }
                else
                {
                    _logger.LogWarning("User not found in database for external ID {UserId}", keycloakUserId);
                }
            }
            else
            {
                _logger.LogWarning("External identity not found for user {UserId}", keycloakUserId);
            }

            _logger.LogInformation("Saga completed successfully for user {UserId}. Deleted by {CallingUserId}", keycloakUserId, callingUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga failed for user deletion {UserId}. Starting compensating transactions. State: KeycloakDeleted={KeycloakDeleted}, DbTransaction={DbTransaction}",
                keycloakUserId, keycloakUserDeleted, databaseTransactionStarted);

            // Execute compensating transactions in reverse order
            await ExecuteDeleteCompensatingTransactionsAsync(deletedKeycloakUser, keycloakUserDeleted, databaseTransactionStarted);

            throw;
        }
    }

    /// <summary>
    /// Executes compensating transactions for user creation rollback
    /// </summary>
    private async Task ExecuteCreateCompensatingTransactionsAsync(
        string? createdUserId,
        string? organizationId,
        bool addedToOrganization,
        bool databaseTransactionStarted)
    {
        // Compensate Step 3: Rollback database transaction
        if (databaseTransactionStarted)
        {
            try
            {
                _logger.LogWarning("Compensating transaction: Rolling back database transaction");
                await _unitOfWork.RollbackAsync();
                _logger.LogInformation("Database transaction rolled back successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rollback database transaction");
            }
        }

        // Compensate Step 2: Remove user from organization
        if (addedToOrganization && createdUserId != null && organizationId != null)
        {
            try
            {
                _logger.LogWarning("Compensating transaction: Removing user {UserId} from organization {OrgId}", createdUserId, organizationId);
                await _kcOrganisationService.RemoveUserFromOrganisationAsync(createdUserId, organizationId);
                _logger.LogInformation("User removed from organization successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove user {UserId} from organization {OrgId}", createdUserId, organizationId);
            }
        }

        // Compensate Step 1: Delete user from Keycloak
        if (createdUserId != null)
        {
            try
            {
                _logger.LogWarning("Compensating transaction: Deleting user {UserId} from Keycloak", createdUserId);
                await _kcUserService.DeleteUserAsync(createdUserId);
                _logger.LogInformation("User deleted from Keycloak successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete user {UserId} from Keycloak", createdUserId);
            }
        }
    }

    /// <summary>
    /// Executes compensating transactions for user deletion rollback
    /// </summary>
    private async Task ExecuteDeleteCompensatingTransactionsAsync(
        UserRepresentation? deletedKeycloakUser,
        bool keycloakUserDeleted,
        bool databaseTransactionStarted)
    {
        // Compensate Step 3: Rollback database transaction
        if (databaseTransactionStarted)
        {
            try
            {
                _logger.LogWarning("Compensating transaction: Rolling back database transaction");
                await _unitOfWork.RollbackAsync();
                _logger.LogInformation("Database transaction rolled back successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rollback database transaction");
            }
        }

        // Compensate Step 2: Restore user to Keycloak (if deleted)
        if (keycloakUserDeleted && deletedKeycloakUser != null)
        {
            try
            {
                _logger.LogWarning("Compensating transaction: Restoring user to Keycloak");
                var restoreModel = new CreateUserModel
                {
                    UserName = deletedKeycloakUser.Username ?? string.Empty,
                    Email = deletedKeycloakUser.Email ?? string.Empty,
                    FirstName = deletedKeycloakUser.FirstName ?? string.Empty,
                    LastName = deletedKeycloakUser.LastName ?? string.Empty,
                    Password = "TEMP_PASSWORD_REQUIRES_RESET" // Temporary password - user will need to reset
                };
                await _kcUserService.CreateUserAsync(restoreModel);
                _logger.LogWarning("User restored to Keycloak. Note: User will need to reset password.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore user to Keycloak. Manual intervention required.");
            }
        }
    }

    /// <summary>
    /// Persists user to database with external identity and tenant relationship
    /// </summary>
    private async Task PersistUserToDatabaseAsync(UserRepresentation userRepresentation, string? organizationId)
    {
        // Look up the pre-seeded Keycloak identity provider
        var keycloakProvider = await _unitOfWork.IdentityProviders.GetByNameAsync("Keycloak");
        if (keycloakProvider == null)
        {
            throw new InvalidOperationException("Keycloak identity provider not found in database. Ensure it is pre-seeded.");
        }

        // Check if user already exists
        var existingUserExternalIdentity = await _unitOfWork.ExternalIdentities.GetByExternalIdentifierAsync(userRepresentation.Id, keycloakProvider.Id);
        if (existingUserExternalIdentity != null)
        {
            _logger.LogWarning("User with Keycloak ID {UserId} already exists in database. Skipping creation.", userRepresentation.Id);
            return;
        }

        // Generate internal GUID for user
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
            Email = userRepresentation.Email ?? string.Empty,
            StatusId = activeStatus.Id
        };
        await _unitOfWork.Users.AddAsync(user);

        // Create ExternalIdentity for user linking internal ID to Keycloak GUID
        var userExternalIdentity = new ExternalIdentity
        {
            ProviderId = keycloakProvider.Id,
            EntityTypeId = ExternalIdentityEntityTypeIds.User,
            EntityId = userId,
            ExternalIdentifier = userRepresentation.Id
        };
        await _unitOfWork.ExternalIdentities.AddAsync(userExternalIdentity);

        // If organization specified, create TenantUser relationship
        if (!string.IsNullOrEmpty(organizationId))
        {
            var tenantExternalIdentity = await _unitOfWork.ExternalIdentities.GetByExternalIdentifierAsync(organizationId, keycloakProvider.Id);
            if (tenantExternalIdentity == null)
            {
                throw new InvalidOperationException($"Tenant with Keycloak ID {organizationId} not found in database.");
            }

            // Get org-user role (default role for users added to organizations)
            var orgUserRole = await _roleService.GetRoleByNameAsync("org-user");
            if (orgUserRole == null)
            {
                throw new InvalidOperationException("org-user role not found in database. Ensure roles are pre-seeded.");
            }

            var tenantUser = new TenantUser
            {
                TenantId = tenantExternalIdentity.EntityId,
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

            // Create UserProfile with the user's name from Keycloak
            var userProfile = new UserProfile
            {
                FirstName = userRepresentation.FirstName ?? string.Empty,
                LastName = userRepresentation.LastName ?? string.Empty
            };
            await _unitOfWork.UserProfiles.AddAsync(userProfile);

            // Create TenantUserProfile linking the profile to this tenant-user relationship
            var tenantUserProfile = new TenantUserProfile
            {
                TenantUserId = tenantUser.Id,
                UserProfileId = userProfile.Id
            };
            await _unitOfWork.TenantUserProfiles.AddAsync(tenantUserProfile);
        }
    }
}