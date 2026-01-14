using IdentityTenantManagement.Constants;
using IdentityTenantManagement.Models.Onboarding;
using IdentityTenantManagementDatabase.Models;
using IdentityTenantManagementDatabase.Repositories;
using IO.Swagger.Model;
using KeycloakAdapter.Models;
using KeycloakAdapter.Services;
using IdentityTenantManagement.Models.Onboarding;

namespace IdentityTenantManagement.Services;

public interface ITenantOrchestrationService
{
    Task<string> CreateTenantAsync(CreateTenantModel model);
    Task<string> InviteUserToTenantAsync(InviteUserModel model);
}

public class TenantOrchestrationService : ITenantOrchestrationService
{
    private readonly IKCOrganisationService _kcOrganisationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKCUserService _kcUserService;
    private readonly ILogger<TenantOrchestrationService> _logger;
    private readonly IRoleService _roleService;

    public TenantOrchestrationService(
        IKCOrganisationService kcOrganisationService,
        IUnitOfWork unitOfWork,
        IKCUserService kcUserService,
        ILogger<TenantOrchestrationService> logger,
        IRoleService roleService)
    {
        _kcOrganisationService = kcOrganisationService;
        _kcUserService = kcUserService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _roleService = roleService;
    }

    public async Task<string> CreateTenantAsync(CreateTenantModel model)
    {
        _logger.LogInformation("Starting tenant creation saga for {TenantName}", model.Name);

        // Track saga state for compensating transactions
        string? createdTenantId = null;
        bool databaseTransactionStarted = false;

        try
        {
            // Step 1: Create tenant in Keycloak
            _logger.LogInformation("Saga Step 1: Creating tenant in Keycloak");
            await _kcOrganisationService.CreateOrgAsync(model);
            var orgRepresentation = await _kcOrganisationService.GetOrganisationByDomain(model.Domain);
            createdTenantId = orgRepresentation.Id;
            _logger.LogInformation("Saga Step 1: Tenant created successfully with ID {TenantId}", createdTenantId);

            // Step 2: Persist to database with transaction
            _logger.LogInformation("Saga Step 2: Persisting tenant to database");
            await _unitOfWork.BeginTransactionAsync();
            databaseTransactionStarted = true;

            await PersistTenantToDatabaseAsync(orgRepresentation);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Saga completed successfully for tenant {TenantName} with ID {TenantId}", model.Name, createdTenantId);
            return createdTenantId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga failed for tenant {TenantName}. Starting compensating transactions. State: TenantId={TenantId}, DbTransaction={DbTransaction}",
                model.Name, createdTenantId, databaseTransactionStarted);

            // Execute compensating transactions in reverse order
            await ExecuteCreateTenantCompensatingTransactionsAsync(createdTenantId, databaseTransactionStarted);

            throw;
        }
    }

    public async Task<string> InviteUserToTenantAsync(InviteUserModel model)
    {
        _logger.LogInformation("Starting user invitation saga for tenant {TenantId}", model.TenantId);

        // Track saga state for compensating transactions
        string? userId = null;
        bool userWasCreated = false;
        bool databaseTransactionStarted = false;

        try
        {
            CreateUserModel cum = new CreateUserModel()
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
            };

            // Step 1: Get or create user in Keycloak (without password for invitations)
            _logger.LogInformation("Saga Step 1: Getting or creating user in Keycloak");
            var existingUser = await _kcUserService.TryGetUserByEmailAsync(cum.Email);
            UserRepresentation userRepresentation;

            if (existingUser != null)
            {
                _logger.LogInformation("User already exists with email: {Email}, using existing user", cum.Email);
                userRepresentation = existingUser;
                userWasCreated = false;
            }
            else
            {
                _logger.LogInformation("User does not exist, creating new user for invitation: {Email}", cum.Email);
                await _kcUserService.CreateUserAsync(cum);
                userRepresentation = await _kcUserService.GetUserByEmailAsync(cum.Email);
                userWasCreated = true;
                await _kcUserService.SendCreatePasswordResetEmailAsync(userRepresentation.Id);
 
            }

            userId = userRepresentation.Id;
            _logger.LogInformation("Saga Step 1: User retrieved/created successfully with ID {UserId}", userId);

            // Step 2: Resolve internal tenant ID from Keycloak tenant ID
            var keycloakProviderId = Guid.Parse("049284C1-FF29-4F28-869F-F64300B69719");
            var tenantExternalIdentities = await _unitOfWork.ExternalIdentities.GetByEntityAsync(ExternalIdentityEntityTypeIds.Tenant, Guid.Parse(model.TenantId));
            var tenantExternalIdentity = tenantExternalIdentities.FirstOrDefault(e => e.ProviderId == keycloakProviderId)?.ExternalIdentifier;

            if (tenantExternalIdentity == null)
            {
                throw new InvalidOperationException($"Could not find Keycloak tenant ID for internal tenant ID {model.TenantId}");
            }

            model.TenantId = tenantExternalIdentity;

            // Step 3: Link user to organization in Keycloak
            _logger.LogInformation("Saga Step 2: Linking user {UserId} to tenant {TenantId} in Keycloak", userId, model.TenantId);
            UserTenantModel utm = new UserTenantModel()
            {
                UserId = userId,
                TenantId = model.TenantId
                
            };
            await _kcOrganisationService.AddUserToOrganisationAsync(utm);
            _logger.LogInformation("Saga Step 2: User linked to tenant successfully");

            // Step 4: Persist user to database with transaction
            _logger.LogInformation("Saga Step 3: Persisting user invitation to database");
            await _unitOfWork.BeginTransactionAsync();
            databaseTransactionStarted = true;

            await PersistInvitedUserToDatabaseAsync(userRepresentation, model);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Saga completed successfully for user invitation {UserId} to tenant {TenantId}", userId, model.TenantId);
            return userId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga failed for user invitation to tenant {TenantId}. Starting compensating transactions. State: UserId={UserId}, UserWasCreated={UserWasCreated}, DbTransaction={DbTransaction}",
                model.TenantId, userId, userWasCreated, databaseTransactionStarted);

            // Execute compensating transactions in reverse order
            await ExecuteInviteUserCompensatingTransactionsAsync(userId, userWasCreated, model.TenantId, databaseTransactionStarted);

            throw;
        }
    }

 

    /// <summary>
    /// Executes compensating transactions for tenant creation rollback
    /// </summary>
    private async Task ExecuteCreateTenantCompensatingTransactionsAsync(
        string? createdTenantId,
        bool databaseTransactionStarted)
    {
        // Compensate Step 2: Rollback database transaction
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

        // Compensate Step 1: Delete tenant from Keycloak
        if (createdTenantId != null)
        {
            try
            {
                _logger.LogWarning("Compensating transaction: Deleting tenant {TenantId} from Keycloak", createdTenantId);
                await _kcOrganisationService.DeleteOrganisationAsync(createdTenantId);
                _logger.LogInformation("Tenant deleted from Keycloak successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete tenant {TenantId} from Keycloak", createdTenantId);
            }
        }
    }

    /// <summary>
    /// Executes compensating transactions for user invitation rollback
    /// </summary>
    private async Task ExecuteInviteUserCompensatingTransactionsAsync(
        string? userId,
        bool userWasCreated,
        string tenantId,
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
        if (userId != null)
        {
            try
            {
                _logger.LogWarning("Compensating transaction: Removing user {UserId} from tenant {TenantId}", userId, tenantId);
                await _kcOrganisationService.RemoveUserFromOrganisationAsync(userId, tenantId);
                _logger.LogInformation("User removed from tenant successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove user {UserId} from tenant {TenantId}", userId, tenantId);
            }
        }

        // Compensate Step 1: Delete user from Keycloak (only if we created it in this saga)
        if (userWasCreated && userId != null)
        {
            try
            {
                _logger.LogWarning("Compensating transaction: Deleting user {UserId} from Keycloak", userId);
                await _kcUserService.DeleteUserAsync(userId);
                _logger.LogInformation("User deleted from Keycloak successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete user {UserId} from Keycloak", userId);
            }
        }
    }

    /// <summary>
    /// Persists tenant to database with external identity
    /// </summary>
    private async Task PersistTenantToDatabaseAsync(OrganizationRepresentation orgRepresentation)
    {
        // Look up the pre-seeded Keycloak identity provider
        var keycloakProvider = await _unitOfWork.IdentityProviders.GetByNameAsync("Keycloak");
        if (keycloakProvider == null)
        {
            throw new InvalidOperationException("Keycloak identity provider not found in database. Ensure it is pre-seeded.");
        }

        // Check if tenant already exists
        var existingTenantExternalIdentity = await _unitOfWork.ExternalIdentities.GetByExternalIdentifierAsync(orgRepresentation.Id, keycloakProvider.Id);
        if (existingTenantExternalIdentity != null)
        {
            _logger.LogWarning("Tenant with Keycloak ID {TenantId} already exists in database. Skipping creation.", orgRepresentation.Id);
            return;
        }

        // Generate internal GUID for tenant
        var tenantId = Guid.NewGuid();

        // Create tenant with internal GUID
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = orgRepresentation.Name,
            Domains = orgRepresentation.Domains
                .Select(d => new TenantDomain
                {
                    Domain = d.Name,
                    IsPrimary = d == orgRepresentation.Domains.First()
                })
                .ToList()
        };
        await _unitOfWork.Tenants.AddAsync(tenant);

        // Create ExternalIdentity for tenant linking internal ID to Keycloak GUID
        var tenantExternalIdentity = new ExternalIdentity
        {
            ProviderId = keycloakProvider.Id,
            EntityTypeId = ExternalIdentityEntityTypeIds.Tenant,
            EntityId = tenantId,
            ExternalIdentifier = orgRepresentation.Id
        };
        await _unitOfWork.ExternalIdentities.AddAsync(tenantExternalIdentity);
    }

    /// <summary>
    /// Persists invited user to database with tenant relationship and user profile
    /// </summary>
    private async Task PersistInvitedUserToDatabaseAsync(UserRepresentation userRepresentation, InviteUserModel model)
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

        // Check if tenant exists
        var tenantExternalIdentity = await _unitOfWork.ExternalIdentities.GetByExternalIdentifierAsync(model.TenantId, keycloakProvider.Id);
        if (tenantExternalIdentity == null)
        {
            throw new InvalidOperationException($"Tenant with Keycloak ID {model.TenantId} not found in database.");
        }

        // Generate internal GUID for user
        var userId = Guid.NewGuid();

        // Get the active status
        var activeStatus = await _unitOfWork.UserStatusTypes.GetByNameAsync("active");
        if (activeStatus == null)
        {
            throw new InvalidOperationException("Active user status type not found in database. Ensure it is pre-seeded.");
        }

        // Create user with internal GUID (basic info - full sync happens via RegistrationProcessorService)
        var user = new User
        {
            Id = userId,
            Email = model.Email ?? string.Empty
        };
        await _unitOfWork.Users.AddAsync(user);

        // Create ExternalIdentity for user
        var userExternalIdentity = new ExternalIdentity
        {
            ProviderId = keycloakProvider.Id,
            EntityTypeId = ExternalIdentityEntityTypeIds.User,
            EntityId = userId,
            ExternalIdentifier = userRepresentation.Id
        };
        await _unitOfWork.ExternalIdentities.AddAsync(userExternalIdentity);

        // Get org-user role (default role for invited users)
        var orgUserRole = await _roleService.GetRoleByNameAsync("org-user");
        if (orgUserRole == null)
        {
            throw new InvalidOperationException("org-user role not found in database. Ensure roles are pre-seeded.");
        }

        // Create TenantUser relationship
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
            LastName = userRepresentation.LastName ?? string.Empty,
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
}