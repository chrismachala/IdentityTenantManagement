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
    Task DeactivateUserInTenantAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task ReactivateUserInTenantAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<(IEnumerable<Models.Users.UserSummaryDto> Users, int TotalCount)> GetUsersAsync(
        Guid tenantId,
        bool includeInactive = false,
        int page = 1,
        int pageSize = 50);
}

public class UserService : IUserService
{
    private readonly IKCUserService _kcUserService;
    private readonly IKCOrganisationService _kcOrganisationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserService> _logger;
    private readonly IRoleService _roleService;
    private readonly IKeycloakSessionService _sessionService;

    public UserService(
        IKCUserService kcUserService,
        IKCOrganisationService kcOrganisationService,
        IUnitOfWork unitOfWork,
        ILogger<UserService> logger,
        IRoleService roleService,
        IKeycloakSessionService sessionService)
    {
        _kcUserService = kcUserService;
        _kcOrganisationService = kcOrganisationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _roleService = roleService;
        _sessionService = sessionService;
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

            // Check if tenant exists in database (by external identity)
            var tenantExternalIdentity = await _unitOfWork.ExternalIdentities.GetByExternalIdentifierAsync(keycloakTenantId, keycloakProvider.Id);
            if (tenantExternalIdentity == null)
            {
                throw new InvalidOperationException($"Tenant with Keycloak ID {keycloakTenantId} not found in database. Ensure the tenant exists before inviting users.");
            }

            var tenantId = tenantExternalIdentity.EntityId;

            // Check if user already exists in database (by external identity)
            var existingUserExternalIdentity = await _unitOfWork.ExternalIdentities.GetByExternalIdentifierAsync(keycloakUserId, keycloakProvider.Id);

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
                Guid userId;
                if (existingUserExternalIdentity != null)
                {
                    userId = existingUserExternalIdentity.EntityId;
                }
                else
                {
                    userId = Guid.NewGuid();

                    // Create user with internal GUID (without name - that goes in profile)
                    var user = new User
                    {
                        Id = userId,
                        Email = email
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
                }

                var existingTenantUser = await _unitOfWork.TenantUsers.GetByTenantAndUserIdAsync(tenantId, userId);
                if (existingTenantUser != null)
                {
                    _logger.LogInformation("User {UserId} is already a member of tenant {TenantId}", userId, tenantId);
                }
                else
                {
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

                    // Get the active status
                    var activeStatus = await _unitOfWork.UserStatusTypes.GetByNameAsync("active");
                    if (activeStatus == null)
                    {
                        throw new InvalidOperationException("Active user status type not found in database. Ensure it is pre-seeded.");
                    }

                    // Create UserProfile with the user's name
                    var userProfile = new UserProfile
                    {
                        FirstName = firstName,
                        LastName = lastName,
                        StatusId = activeStatus.Id
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

    /// <summary>
    /// Deactivates a user in a specific tenant using the Saga pattern to ensure consistency
    /// between database and Keycloak. Operations are performed atomically - if any step fails,
    /// all changes are rolled back.
    ///
    /// Saga Steps:
    /// 1. Mark user inactive in database (not committed)
    /// 2. Revoke all Keycloak sessions
    /// 3. Remove user from Keycloak organization
    /// 4. Commit database changes
    ///
    /// If any step fails, previous steps are compensated (rolled back).
    /// </summary>
    /// <param name="tenantId">The internal tenant ID</param>
    /// <param name="userId">The internal user ID</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown</param>
    public async Task DeactivateUserInTenantAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[SAGA] Starting deactivation saga for user {UserId} in tenant {TenantId}", userId, tenantId);

        // Saga state tracking
        bool sessionsRevoked = false;
        bool removedFromOrganization = false;
        string? keycloakUserId = null;
        string? keycloakOrgId = null;

        try
        {
            // ============================================================
            // STEP 0: Validation and data gathering (before transaction)
            // ============================================================

            // Get TenantUser relationship using internal IDs
            var tenantUser = await _unitOfWork.TenantUsers.GetByTenantAndUserIdAsync(tenantId, userId);
            if (tenantUser == null)
            {
                throw new InvalidOperationException($"User {userId} is not a member of tenant {tenantId}.");
            }

            // Get TenantUserProfile
            var tenantUserProfile = await _unitOfWork.TenantUserProfiles.GetByTenantUserIdAsync(tenantUser.Id);
            if (tenantUserProfile == null)
            {
                throw new InvalidOperationException($"TenantUserProfile not found for TenantUser {tenantUser.Id}.");
            }

            // Get UserProfile
            var userProfile = await _unitOfWork.UserProfiles.GetByIdAsync(tenantUserProfile.UserProfileId);
            if (userProfile == null)
            {
                throw new InvalidOperationException($"UserProfile {tenantUserProfile.UserProfileId} not found.");
            }

            // Get inactive status
            var inactiveStatus = await _unitOfWork.UserStatusTypes.GetByNameAsync("inactive");
            if (inactiveStatus == null)
            {
                throw new InvalidOperationException("Inactive user status type not found in database.");
            }

            // Get Keycloak external IDs BEFORE starting transaction
            var keycloakProviderId = Guid.Parse("049284C1-FF29-4F28-869F-F64300B69719");
            var userExternalIdentities = await _unitOfWork.ExternalIdentities
                .GetByEntityAsync(ExternalIdentityEntityTypeIds.User, userId);
            var userExternalIdentity = userExternalIdentities.FirstOrDefault(e => e.ProviderId == keycloakProviderId);

            var tenantExternalIdentities = await _unitOfWork.ExternalIdentities
                .GetByEntityAsync(ExternalIdentityEntityTypeIds.Tenant, tenantId);
            var tenantExternalIdentity = tenantExternalIdentities.FirstOrDefault(e => e.ProviderId == keycloakProviderId);

            if (userExternalIdentity == null)
            {
                _logger.LogWarning("No Keycloak external identity found for user {UserId}. Deactivation will proceed without Keycloak operations.", userId);
            }

            if (tenantExternalIdentity == null)
            {
                _logger.LogWarning("No Keycloak external identity found for tenant {TenantId}. Deactivation will proceed without Keycloak operations.", tenantId);
            }

            keycloakUserId = userExternalIdentity?.ExternalIdentifier;
            keycloakOrgId = tenantExternalIdentity?.ExternalIdentifier;

            // ============================================================
            // STEP 1: Begin database transaction and mark user inactive
            // ============================================================

            _logger.LogInformation("[SAGA] Step 1: Beginning database transaction");
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var now = DateTime.UtcNow;
                var previousStatusId = userProfile.StatusId;

                // Mark UserProfile as inactive
                userProfile.StatusId = inactiveStatus.Id;
                userProfile.UpdatedAt = now;
                await _unitOfWork.UserProfiles.UpdateAsync(userProfile);

                _logger.LogInformation("[SAGA] Marked UserProfile {ProfileId} as inactive for user {UserId}", userProfile.Id, userId);

                // Set TenantUserProfile.InactiveAt timestamp
                tenantUserProfile.InactiveAt = now;
                await _unitOfWork.TenantUserProfiles.UpdateAsync(tenantUserProfile);

                _logger.LogInformation("[SAGA] Set InactiveAt timestamp for TenantUserProfile {ProfileId}", tenantUserProfile.Id);

                // Check if user is now inactive across ALL tenants and mark globally inactive if so
                await CheckAndMarkGloballyInactiveAsync(userId, inactiveStatus.Id, now);

                // Log the deactivation action (using internal IDs for database records)
                await _unitOfWork.AuditLogs.LogAsync(
                    action: "user.deactivated",
                    resourceType: "User",
                    resourceId: userId.ToString(),
                    actorUserId: null,
                    actorDisplayName: "system",
                    tenantId: tenantId,
                    oldValues: $"{{\"statusId\":\"{previousStatusId}\"}}",
                    newValues: $"{{\"statusId\":\"{inactiveStatus.Id}\",\"inactiveAt\":\"{now:O}\"}}");

                _logger.LogInformation("[SAGA] Created audit log entry for user deactivation");
                _logger.LogInformation("[SAGA] Step 1: Database changes prepared (not committed)");

                // ============================================================
                // STEP 2: Revoke Keycloak sessions (compensatable)
                // ============================================================

                if (keycloakUserId != null)
                {
                    _logger.LogInformation("[SAGA] Step 2: Revoking Keycloak sessions for user {KeycloakId}", keycloakUserId);

                    try
                    {
                        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        sessionCts.CancelAfter(TimeSpan.FromSeconds(30));

                        var revoked = await _sessionService.RevokeAllUserSessionsAsync(keycloakUserId, sessionCts.Token);

                        if (!revoked)
                        {
                            throw new InvalidOperationException($"Failed to revoke sessions for user {userId}");
                        }

                        sessionsRevoked = true;
                        _logger.LogInformation("[SAGA] Step 2: Successfully revoked all sessions for user {UserId}", userId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[SAGA] Step 2 FAILED: Error revoking sessions for user {UserId}", userId);
                        throw new InvalidOperationException($"Saga failed at step 2 (session revocation): {ex.Message}", ex);
                    }
                }
                else
                {
                    _logger.LogInformation("[SAGA] Step 2: Skipped (no Keycloak user ID)");
                }

                // ============================================================
                // STEP 3: Remove user from Keycloak organization (compensatable)
                // ============================================================

                if (keycloakUserId != null && keycloakOrgId != null)
                {
                    _logger.LogInformation("[SAGA] Step 3: Removing user {KeycloakUserId} from organization {KeycloakOrgId}",
                        keycloakUserId, keycloakOrgId);

                    try
                    {
                        await _kcOrganisationService.RemoveUserFromOrganisationAsync(keycloakUserId, keycloakOrgId);
                        removedFromOrganization = true;
                        _logger.LogInformation("[SAGA] Step 3: Successfully removed user {UserId} from Keycloak organization {TenantId}",
                            userId, tenantId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[SAGA] Step 3 FAILED: Error removing user {UserId} from Keycloak organization {TenantId}",
                            userId, tenantId);
                        throw new InvalidOperationException($"Saga failed at step 3 (organization removal): {ex.Message}", ex);
                    }
                }
                else
                {
                    _logger.LogInformation("[SAGA] Step 3: Skipped (no Keycloak identities)");
                }

                // ============================================================
                // STEP 4: Commit database transaction (final step)
                // ============================================================

                _logger.LogInformation("[SAGA] Step 4: Committing database transaction");

                try
                {
                    await _unitOfWork.CommitAsync(cancellationToken);
                    _logger.LogInformation("[SAGA] Step 4: Database changes committed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SAGA] Step 4 FAILED: Error committing database transaction");
                    throw new InvalidOperationException($"Saga failed at step 4 (database commit): {ex.Message}", ex);
                }

                _logger.LogInformation("[SAGA] Successfully deactivated user {UserId} in tenant {TenantId}", userId, tenantId);
            }
            catch (Exception ex)
            {
                // ============================================================
                // COMPENSATION: Rollback all completed steps
                // ============================================================

                _logger.LogError(ex, "[SAGA] Saga failed, initiating compensation (rollback)");

                // Step 4 compensation: Rollback database transaction
                try
                {
                    _logger.LogInformation("[SAGA COMPENSATION] Rolling back database transaction");
                    await _unitOfWork.RollbackAsync(cancellationToken);
                    _logger.LogInformation("[SAGA COMPENSATION] Database transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "[SAGA COMPENSATION] CRITICAL: Failed to rollback database transaction");
                }

                // Step 3 compensation: Re-add user to Keycloak organization (if removed)
                if (removedFromOrganization && keycloakUserId != null && keycloakOrgId != null)
                {
                    try
                    {
                        _logger.LogInformation("[SAGA COMPENSATION] Re-adding user {KeycloakUserId} to organization {KeycloakOrgId}",
                            keycloakUserId, keycloakOrgId);

                        await _kcOrganisationService.AddUserToOrganisationAsync(new KeycloakAdapter.Models.UserTenantModel
                        {
                            UserId = keycloakUserId,
                            TenantId = keycloakOrgId
                        });

                        _logger.LogInformation("[SAGA COMPENSATION] Successfully re-added user to organization");
                    }
                    catch (Exception compensationEx)
                    {
                        _logger.LogError(compensationEx,
                            "[SAGA COMPENSATION] CRITICAL: Failed to re-add user {UserId} to organization {TenantId}. Manual intervention required!",
                            userId, tenantId);
                    }
                }

                // Step 2 compensation: Cannot restore sessions (sessions are ephemeral)
                if (sessionsRevoked)
                {
                    _logger.LogWarning("[SAGA COMPENSATION] Sessions were revoked but cannot be restored. User will need to re-authenticate.");
                }

                _logger.LogError("[SAGA] Deactivation saga failed and was rolled back for user {UserId} in tenant {TenantId}",
                    userId, tenantId);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SAGA] Error in deactivation saga for user {UserId} in tenant {TenantId}", userId, tenantId);
            throw;
        }
    }

    /// <summary>
    /// Reactivates a previously deactivated user in a specific tenant using the Saga pattern
    /// to ensure consistency between database and Keycloak.
    ///
    /// Saga Steps:
    /// 1. Mark user active in database (not committed)
    /// 2. Re-add user to Keycloak organization
    /// 3. Commit database changes
    ///
    /// If any step fails, previous steps are compensated (rolled back).
    /// </summary>
    /// <param name="tenantId">The internal tenant ID</param>
    /// <param name="userId">The internal user ID</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown</param>
    public async Task ReactivateUserInTenantAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[SAGA] Starting reactivation saga for user {UserId} in tenant {TenantId}", userId, tenantId);

        // Saga state tracking
        bool addedToOrganization = false;
        string? keycloakUserId = null;
        string? keycloakOrgId = null;

        try
        {
            // ============================================================
            // STEP 0: Validation and data gathering (before transaction)
            // ============================================================

            // Get TenantUser relationship using internal IDs
            var tenantUser = await _unitOfWork.TenantUsers.GetByTenantAndUserIdAsync(tenantId, userId);
            if (tenantUser == null)
            {
                throw new InvalidOperationException($"User {userId} is not a member of tenant {tenantId}.");
            }

            // Get TenantUserProfile
            var tenantUserProfile = await _unitOfWork.TenantUserProfiles.GetByTenantUserIdAsync(tenantUser.Id);
            if (tenantUserProfile == null)
            {
                throw new InvalidOperationException($"TenantUserProfile not found for TenantUser {tenantUser.Id}.");
            }

            // Get UserProfile
            var userProfile = await _unitOfWork.UserProfiles.GetByIdAsync(tenantUserProfile.UserProfileId);
            if (userProfile == null)
            {
                throw new InvalidOperationException($"UserProfile {tenantUserProfile.UserProfileId} not found.");
            }

            // Get active status
            var activeStatus = await _unitOfWork.UserStatusTypes.GetByNameAsync("active");
            if (activeStatus == null)
            {
                throw new InvalidOperationException("Active user status type not found in database.");
            }

            // Get Keycloak external IDs BEFORE starting transaction
            var keycloakProviderId = Guid.Parse("049284C1-FF29-4F28-869F-F64300B69719");
            var userExternalIdentities = await _unitOfWork.ExternalIdentities
                .GetByEntityAsync(ExternalIdentityEntityTypeIds.User, userId);
            var userExternalIdentity = userExternalIdentities.FirstOrDefault(e => e.ProviderId == keycloakProviderId);

            var tenantExternalIdentities = await _unitOfWork.ExternalIdentities
                .GetByEntityAsync(ExternalIdentityEntityTypeIds.Tenant, tenantId);
            var tenantExternalIdentity = tenantExternalIdentities.FirstOrDefault(e => e.ProviderId == keycloakProviderId);

            if (userExternalIdentity == null)
            {
                _logger.LogWarning("No Keycloak external identity found for user {UserId}. Reactivation will proceed without Keycloak operations.", userId);
            }

            if (tenantExternalIdentity == null)
            {
                _logger.LogWarning("No Keycloak external identity found for tenant {TenantId}. Reactivation will proceed without Keycloak operations.", tenantId);
            }

            keycloakUserId = userExternalIdentity?.ExternalIdentifier;
            keycloakOrgId = tenantExternalIdentity?.ExternalIdentifier;

            // ============================================================
            // STEP 1: Begin database transaction and mark user active
            // ============================================================

            _logger.LogInformation("[SAGA] Step 1: Beginning database transaction");
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var now = DateTime.UtcNow;
                var previousStatusId = userProfile.StatusId;
                var previousInactiveAt = tenantUserProfile.InactiveAt;

                // Mark UserProfile as active
                userProfile.StatusId = activeStatus.Id;
                userProfile.UpdatedAt = now;
                await _unitOfWork.UserProfiles.UpdateAsync(userProfile);

                _logger.LogInformation("[SAGA] Marked UserProfile {ProfileId} as active for user {UserId}", userProfile.Id, userId);

                // Clear TenantUserProfile.InactiveAt timestamp
                tenantUserProfile.InactiveAt = null;
                await _unitOfWork.TenantUserProfiles.UpdateAsync(tenantUserProfile);

                _logger.LogInformation("[SAGA] Cleared InactiveAt timestamp for TenantUserProfile {ProfileId}", tenantUserProfile.Id);

                // Check if user should have global inactive state cleared
                await CheckAndClearGloballyInactiveAsync(userId, activeStatus.Id);

                // Log the reactivation action (using internal IDs for database records)
                await _unitOfWork.AuditLogs.LogAsync(
                    action: "user.reactivated",
                    resourceType: "User",
                    resourceId: userId.ToString(),
                    actorUserId: null,
                    actorDisplayName: "system",
                    tenantId: tenantId,
                    oldValues: $"{{\"statusId\":\"{previousStatusId}\",\"inactiveAt\":\"{previousInactiveAt:O}\"}}",
                    newValues: $"{{\"statusId\":\"{activeStatus.Id}\",\"inactiveAt\":null}}");

                _logger.LogInformation("[SAGA] Created audit log entry for user reactivation");
                _logger.LogInformation("[SAGA] Step 1: Database changes prepared (not committed)");

                // ============================================================
                // STEP 2: Re-add user to Keycloak organization (compensatable)
                // ============================================================

                if (keycloakUserId != null && keycloakOrgId != null)
                {
                    _logger.LogInformation("[SAGA] Step 2: Re-adding user {KeycloakUserId} to organization {KeycloakOrgId}",
                        keycloakUserId, keycloakOrgId);

                    try
                    {
                        await _kcOrganisationService.AddUserToOrganisationAsync(new KeycloakAdapter.Models.UserTenantModel
                        {
                            UserId = keycloakUserId,
                            TenantId = keycloakOrgId
                        });

                        addedToOrganization = true;
                        _logger.LogInformation("[SAGA] Step 2: Successfully re-added user {UserId} to Keycloak organization {TenantId}",
                            userId, tenantId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[SAGA] Step 2 FAILED: Error re-adding user {UserId} to Keycloak organization {TenantId}",
                            userId, tenantId);
                        throw new InvalidOperationException($"Saga failed at step 2 (organization re-addition): {ex.Message}", ex);
                    }
                }
                else
                {
                    _logger.LogInformation("[SAGA] Step 2: Skipped (no Keycloak identities)");
                }

                // ============================================================
                // STEP 3: Commit database transaction (final step)
                // ============================================================

                _logger.LogInformation("[SAGA] Step 3: Committing database transaction");

                try
                {
                    await _unitOfWork.CommitAsync(cancellationToken);
                    _logger.LogInformation("[SAGA] Step 3: Database changes committed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SAGA] Step 3 FAILED: Error committing database transaction");
                    throw new InvalidOperationException($"Saga failed at step 3 (database commit): {ex.Message}", ex);
                }

                _logger.LogInformation("[SAGA] Successfully reactivated user {UserId} in tenant {TenantId}", userId, tenantId);
            }
            catch (Exception ex)
            {
                // ============================================================
                // COMPENSATION: Rollback all completed steps
                // ============================================================

                _logger.LogError(ex, "[SAGA] Saga failed, initiating compensation (rollback)");

                // Step 3 compensation: Rollback database transaction
                try
                {
                    _logger.LogInformation("[SAGA COMPENSATION] Rolling back database transaction");
                    await _unitOfWork.RollbackAsync(cancellationToken);
                    _logger.LogInformation("[SAGA COMPENSATION] Database transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "[SAGA COMPENSATION] CRITICAL: Failed to rollback database transaction");
                }

                // Step 2 compensation: Remove user from Keycloak organization (if added)
                if (addedToOrganization && keycloakUserId != null && keycloakOrgId != null)
                {
                    try
                    {
                        _logger.LogInformation("[SAGA COMPENSATION] Removing user {KeycloakUserId} from organization {KeycloakOrgId}",
                            keycloakUserId, keycloakOrgId);

                        await _kcOrganisationService.RemoveUserFromOrganisationAsync(keycloakUserId, keycloakOrgId);

                        _logger.LogInformation("[SAGA COMPENSATION] Successfully removed user from organization");
                    }
                    catch (Exception compensationEx)
                    {
                        _logger.LogError(compensationEx,
                            "[SAGA COMPENSATION] CRITICAL: Failed to remove user {UserId} from organization {TenantId}. Manual intervention required!",
                            userId, tenantId);
                    }
                }

                _logger.LogError("[SAGA] Reactivation saga failed and was rolled back for user {UserId} in tenant {TenantId}",
                    userId, tenantId);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SAGA] Error in reactivation saga for user {UserId} in tenant {TenantId}", userId, tenantId);
            throw;
        }
    }

    /// <summary>
    /// Gets a paginated list of users for a tenant with optional filtering of inactive users.
    /// </summary>
    /// <param name="tenantId">The internal tenant ID</param>
    /// <param name="includeInactive">Whether to include inactive users in the results</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Tuple of user DTOs and total count</returns>
    public async Task<(IEnumerable<Models.Users.UserSummaryDto> Users, int TotalCount)> GetUsersAsync(
        Guid tenantId,
        bool includeInactive = false,
        int page = 1,
        int pageSize = 50)
    {
        _logger.LogInformation("Getting users for tenant {TenantId} (includeInactive: {IncludeInactive}, page: {Page}, pageSize: {PageSize})",
            tenantId, includeInactive, page, pageSize);

        try
        {
            var (tenantUsers, totalCount) = await _unitOfWork.TenantUsers.GetUsersAsync(
                tenantId,
                includeInactive,
                page,
                pageSize);

            var userDtos = new List<Models.Users.UserSummaryDto>();

            foreach (var tenantUser in tenantUsers)
            {
                // Get the user's profile for this tenant
                var tenantUserProfile = await _unitOfWork.TenantUserProfiles.GetByTenantUserIdAsync(tenantUser.Id);

                if (tenantUserProfile == null)
                {
                    _logger.LogWarning("No profile found for TenantUser {TenantUserId}", tenantUser.Id);
                    continue;
                }

                var userProfile = await _unitOfWork.UserProfiles.GetByIdAsync(tenantUserProfile.UserProfileId);

                if (userProfile == null)
                {
                    _logger.LogWarning("UserProfile {ProfileId} not found", tenantUserProfile.UserProfileId);
                    continue;
                }

                // Get inactive status to determine if user is active
                var inactiveStatus = await _unitOfWork.UserStatusTypes.GetByNameAsync("inactive");
                var isActive = inactiveStatus == null || userProfile.StatusId != inactiveStatus.Id;

                var userDto = new Models.Users.UserSummaryDto
                {
                    UserId = tenantUser.UserId,
                    Email = tenantUser.User.Email,
                    FirstName = userProfile.FirstName,
                    LastName = userProfile.LastName,
                    Username = tenantUser.User.Email, // Using email as username for now
                    IsActive = isActive,
                    InactiveAt = tenantUserProfile.InactiveAt,
                    JoinedAt = tenantUser.JoinedAt,
                    Roles = tenantUser.TenantUserRoles.Select(tur => tur.Role.Name).ToList()
                };

                userDtos.Add(userDto);
            }

            _logger.LogInformation("Retrieved {Count} users out of {Total} total for tenant {TenantId}",
                userDtos.Count, totalCount, tenantId);

            return (userDtos, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users for tenant {TenantId}", tenantId);
            throw;
        }
    }

    /// <summary>
    /// Checks if a user is inactive across ALL tenants they belong to.
    /// If so, marks the user as globally inactive by setting User.GloballyInactiveAt timestamp.
    /// </summary>
    /// <param name="userId">The internal user ID (database GUID, NOT Keycloak ID)</param>
    /// <param name="inactiveStatusId">The inactive status type ID</param>
    /// <param name="timestamp">The timestamp to use for GloballyInactiveAt</param>
    private async Task CheckAndMarkGloballyInactiveAsync(Guid userId, Guid inactiveStatusId, DateTime timestamp)
    {
        _logger.LogInformation("Checking if user {UserId} is now globally inactive", userId);

        try
        {
            // Get all TenantUser relationships for this user
            var allTenantUsers = await _unitOfWork.TenantUsers.GetByUserIdAsync(userId);

            if (!allTenantUsers.Any())
            {
                _logger.LogWarning("User {UserId} has no tenant memberships", userId);
                return;
            }

            // Check each tenant membership to see if ALL are inactive
            var allInactive = true;

            foreach (var tenantUser in allTenantUsers)
            {
                // Get the user's profile for this tenant
                var tenantUserProfile = await _unitOfWork.TenantUserProfiles.GetByTenantUserIdAsync(tenantUser.Id);

                if (tenantUserProfile == null)
                {
                    _logger.LogWarning("No TenantUserProfile found for TenantUser {TenantUserId}", tenantUser.Id);
                    continue;
                }

                var userProfile = await _unitOfWork.UserProfiles.GetByIdAsync(tenantUserProfile.UserProfileId);

                if (userProfile == null)
                {
                    _logger.LogWarning("UserProfile {ProfileId} not found", tenantUserProfile.UserProfileId);
                    continue;
                }

                // If ANY profile is not inactive, user is still active in at least one tenant
                if (userProfile.StatusId != inactiveStatusId)
                {
                    allInactive = false;
                    _logger.LogInformation("User {UserId} is still active in tenant {TenantId}", userId, tenantUser.TenantId);
                    break;
                }
            }

            // If user is inactive in ALL tenants, mark as globally inactive
            if (allInactive)
            {
                _logger.LogInformation("User {UserId} is now inactive across all tenants. Marking as globally inactive.", userId);

                await _unitOfWork.Users.MarkAsGloballyInactiveAsync(userId);

                // Log the global inactive state change
                await _unitOfWork.AuditLogs.LogAsync(
                    action: "user.globally_inactive",
                    resourceType: "User",
                    resourceId: userId.ToString(),
                    actorUserId: null,
                    actorDisplayName: "system",
                    tenantId: null,
                    oldValues: "{\"globallyInactiveAt\":null}",
                    newValues: $"{{\"globallyInactiveAt\":\"{timestamp:O}\"}}");

                _logger.LogInformation("Marked user {UserId} as globally inactive at {Timestamp}", userId, timestamp);
            }
            else
            {
                _logger.LogInformation("User {UserId} is still active in at least one tenant", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking global inactive state for user {UserId}", userId);
            // Don't throw - this is a non-critical operation that shouldn't block the main deactivation
            // The user is already marked inactive in the specific tenant
        }
    }

    /// <summary>
    /// Checks if a user who was globally inactive is now active in at least one tenant.
    /// If so, clears the User.GloballyInactiveAt timestamp.
    /// </summary>
    /// <param name="userId">The internal user ID (database GUID, NOT Keycloak ID)</param>
    /// <param name="activeStatusId">The active status type ID</param>
    private async Task CheckAndClearGloballyInactiveAsync(Guid userId, Guid activeStatusId)
    {
        _logger.LogInformation("Checking if globally inactive user {UserId} should be cleared", userId);

        try
        {
            // Get the user to check if they're marked as globally inactive
            var user = await _unitOfWork.Users.GetByIdAsync(userId);

            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return;
            }

            // If user is not globally inactive, nothing to do
            if (!user.GloballyInactiveAt.HasValue)
            {
                _logger.LogInformation("User {UserId} is not marked as globally inactive", userId);
                return;
            }

            // Get all TenantUser relationships for this user
            var allTenantUsers = await _unitOfWork.TenantUsers.GetByUserIdAsync(userId);

            if (!allTenantUsers.Any())
            {
                _logger.LogWarning("User {UserId} has no tenant memberships", userId);
                return;
            }

            // Check if user is now active in at least one tenant
            var hasActiveProfile = false;

            foreach (var tenantUser in allTenantUsers)
            {
                // Get the user's profile for this tenant
                var tenantUserProfile = await _unitOfWork.TenantUserProfiles.GetByTenantUserIdAsync(tenantUser.Id);

                if (tenantUserProfile == null)
                {
                    _logger.LogWarning("No TenantUserProfile found for TenantUser {TenantUserId}", tenantUser.Id);
                    continue;
                }

                var userProfile = await _unitOfWork.UserProfiles.GetByIdAsync(tenantUserProfile.UserProfileId);

                if (userProfile == null)
                {
                    _logger.LogWarning("UserProfile {ProfileId} not found", tenantUserProfile.UserProfileId);
                    continue;
                }

                // If ANY profile is active, user should not be globally inactive
                if (userProfile.StatusId == activeStatusId)
                {
                    hasActiveProfile = true;
                    _logger.LogInformation("User {UserId} is now active in tenant {TenantId}", userId, tenantUser.TenantId);
                    break;
                }
            }

            // If user has at least one active profile, clear global inactive state
            if (hasActiveProfile)
            {
                _logger.LogInformation("User {UserId} is now active in at least one tenant. Clearing globally inactive state.", userId);

                await _unitOfWork.Users.ClearGloballyInactiveStatusAsync(userId);

                // Log the global inactive state cleared
                await _unitOfWork.AuditLogs.LogAsync(
                    action: "user.globally_active",
                    resourceType: "User",
                    resourceId: userId.ToString(),
                    actorUserId: null,
                    actorDisplayName: "system",
                    tenantId: null,
                    oldValues: $"{{\"globallyInactiveAt\":\"{user.GloballyInactiveAt:O}\"}}",
                    newValues: "{\"globallyInactiveAt\":null}");

                _logger.LogInformation("Cleared globally inactive state for user {UserId}", userId);
            }
            else
            {
                _logger.LogInformation("User {UserId} is still inactive in all tenants", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking global inactive state for user {UserId}", userId);
            // Don't throw - this is a non-critical operation that shouldn't block the main reactivation
            // The user is already marked active in the specific tenant
        }
    }
}
