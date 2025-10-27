using IdentityTenantManagement.Constants;
using IdentityTenantManagementDatabase.Models;
using IdentityTenantManagementDatabase.Repositories;
using IO.Swagger.Model;
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
        _logger.LogInformation("Starting update saga for user {UserId}", userId);

        // Track saga state for compensating transactions
        UserRepresentation? originalKeycloakUser = null;
        bool keycloakUpdated = false;
        bool databaseTransactionStarted = false;

        try
        {
            // Step 1: Capture original Keycloak state for potential rollback
            _logger.LogInformation("Saga Step 1: Capturing original Keycloak user state for {UserId}", userId);
            originalKeycloakUser = await _kcUserService.GetUserByIdAsync(userId);
            _logger.LogInformation("Saga Step 1: Original state captured - Email: {Email}, FirstName: {FirstName}, LastName: {LastName}",
                originalKeycloakUser.Email, originalKeycloakUser.FirstName, originalKeycloakUser.LastName);

            // Step 2: Update user in Keycloak
            _logger.LogInformation("Saga Step 2: Updating user {UserId} in Keycloak", userId);
            await _kcUserService.UpdateUserAsync(userId, model);
            keycloakUpdated = true;
            _logger.LogInformation("Saga Step 2: User updated successfully in Keycloak");

            // Step 3: Update user in database with transaction
            _logger.LogInformation("Saga Step 3: Updating user in database");
            var keycloakProviderId = Guid.Parse("049284C1-FF29-4F28-869F-F64300B69719");
            var userExternalIdentity = await _unitOfWork.ExternalIdentities.GetByExternalIdentifierAsync(userId, keycloakProviderId);

            if (userExternalIdentity == null)
            {
                throw new InvalidOperationException($"External identity not found for Keycloak user {userId}. User may not be synchronized to database.");
            }

            var user = await _unitOfWork.Users.GetByIdAsync(userExternalIdentity.EntityId);
            if (user == null)
            {
                throw new InvalidOperationException($"User not found in database for external ID {userId}. Data inconsistency detected.");
            }

            // Begin database transaction
            await _unitOfWork.BeginTransactionAsync();
            databaseTransactionStarted = true;

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.Email = model.Email;

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Saga completed successfully for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga failed for user {UserId}. Starting compensating transactions. State: KeycloakUpdated={KeycloakUpdated}, DbTransaction={DbTransaction}",
                userId, keycloakUpdated, databaseTransactionStarted);

            // Execute compensating transactions in reverse order
            await ExecuteUpdateCompensatingTransactionsAsync(userId, originalKeycloakUser, keycloakUpdated, databaseTransactionStarted);

            throw;
        }
    }

    /// <summary>
    /// Executes compensating transactions to rollback update changes
    /// </summary>
    private async Task ExecuteUpdateCompensatingTransactionsAsync(
        string userId,
        UserRepresentation? originalKeycloakUser,
        bool keycloakUpdated,
        bool databaseTransactionStarted)
    {
        // Compensate Step 3: Rollback database transaction
        if (databaseTransactionStarted)
        {
            try
            {
                _logger.LogWarning("Compensating transaction: Rolling back database transaction for user {UserId}", userId);
                await _unitOfWork.RollbackAsync();
                _logger.LogInformation("Database transaction rolled back successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rollback database transaction for user {UserId}", userId);
            }
        }

        // Compensate Step 2: Restore original Keycloak user state
        if (keycloakUpdated && originalKeycloakUser != null)
        {
            try
            {
                _logger.LogWarning("Compensating transaction: Restoring original Keycloak user state for {UserId}", userId);
                var restoreModel = new CreateUserModel
                {
                    FirstName = originalKeycloakUser.FirstName ?? string.Empty,
                    LastName = originalKeycloakUser.LastName ?? string.Empty,
                    Email = originalKeycloakUser.Email ?? string.Empty,
                    UserName = originalKeycloakUser.Username ?? string.Empty,
                    Password = string.Empty // Password not needed for update
                };
                await _kcUserService.UpdateUserAsync(userId, restoreModel);
                _logger.LogInformation("Keycloak user state restored successfully for {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore Keycloak user state for {UserId}. Manual intervention may be required.", userId);
            }
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

                // Create user with internal GUID
                var user = new User
                {
                    Id = userId,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
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