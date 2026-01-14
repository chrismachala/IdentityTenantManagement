using IdentityTenantManagement.Constants;
using IdentityTenantManagement.Models.Onboarding;
using IdentityTenantManagementDatabase.Models;
using IdentityTenantManagementDatabase.Repositories;
using IO.Swagger.Model;
using KeycloakAdapter.Models;
using KeycloakAdapter.Services;

namespace IdentityTenantManagement.Services;

public interface IOnboardingService
{
    Task OnboardOrganisationAsync(TenantUserOnboardingModel model);
}

public class OnboardingService : IOnboardingService
{
    private readonly IKCOrganisationService _kcOrganisationService;
    private readonly IKCUserService _kcUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OnboardingService> _logger;
    private readonly IRoleService _roleService;

    public OnboardingService(
        IKCOrganisationService kcOrganisationService,
        IKCUserService kcUserService,
        IUnitOfWork unitOfWork,
        ILogger<OnboardingService> logger,
        IRoleService roleService)
    {
        _kcOrganisationService = kcOrganisationService;
        _kcUserService = kcUserService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _roleService = roleService;
    }

    public async Task OnboardOrganisationAsync(TenantUserOnboardingModel model)
    {
        _logger.LogInformation("Starting onboarding saga for user {Email} and tenant {TenantName}",
            model.CreateUserModel.Email, model.CreateTenantModel.Name);

        // Track saga state for compensating transactions
        string? userId = null;
        bool userWasCreated = false;
        string? createdOrgId = null;
        bool userLinkedToOrg = false;
        bool databaseTransactionStarted = false;

        try
        {
            // Step 1: Get or create user in Keycloak
            _logger.LogInformation("Saga Step 1: Getting or creating user in Keycloak");
            var existingUser = await _kcUserService.TryGetUserByEmailAsync(model.CreateUserModel.Email);
            UserRepresentation userRepresentation;

            if (existingUser != null)
            {
                _logger.LogInformation("User already exists with email: {Email}, using existing user", model.CreateUserModel.Email);
                userRepresentation = existingUser;
                userWasCreated = false;
            }
            else
            {
                _logger.LogInformation("User does not exist, creating new user: {Email}", model.CreateUserModel.Email);
                await _kcUserService.CreateUserAsync(model.CreateUserModel);
                userRepresentation = await _kcUserService.GetUserByEmailAsync(model.CreateUserModel.Email);
                userWasCreated = true;
            }

            userId = userRepresentation.Id;
            _logger.LogInformation("Saga Step 1: User retrieved/created successfully with ID {UserId}", userId);

            // Step 2: Create organization in Keycloak
            _logger.LogInformation("Saga Step 2: Creating organisation in Keycloak");
            await _kcOrganisationService.CreateOrgAsync(model.CreateTenantModel);

            OrganizationRepresentation organizationRepresentation = await _kcOrganisationService.GetOrganisationByDomain(model.CreateTenantModel.Domain);
            createdOrgId = organizationRepresentation.Id;
            _logger.LogInformation("Saga Step 2: Organisation created successfully with ID {OrgId}", createdOrgId);

            // Step 3: Link user to organization
            _logger.LogInformation("Saga Step 3: Linking user {UserId} to organisation {OrgId}", userId, createdOrgId);
            UserTenantModel orgUser = new UserTenantModel
            {
                UserId = userRepresentation.Id,
                TenantId = organizationRepresentation.Id
            };
            await _kcOrganisationService.AddUserToOrganisationAsync(orgUser);
            userLinkedToOrg = true;
            _logger.LogInformation("Saga Step 3: User linked to organisation successfully");

            // Step 4: Persist to database with transaction
            _logger.LogInformation("Saga Step 4: Persisting to database");
            await _unitOfWork.BeginTransactionAsync();
            databaseTransactionStarted = true;

            await AddUserAndTenantAsync(userRepresentation, organizationRepresentation);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Saga completed successfully for user {Email} and tenant {TenantName}",
                model.CreateUserModel.Email, model.CreateTenantModel.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga failed. Starting compensating transactions. State: UserId={UserId}, UserWasCreated={UserWasCreated}, OrgId={OrgId}, UserLinked={UserLinked}, DbTransaction={DbTransaction}",
                userId, userWasCreated, createdOrgId, userLinkedToOrg, databaseTransactionStarted);

            // Execute compensating transactions in reverse order
            await ExecuteCompensatingTransactionsAsync(userId, userWasCreated, createdOrgId, userLinkedToOrg, databaseTransactionStarted);

            throw;
        }
    }

    /// <summary>
    /// Executes compensating transactions to rollback Keycloak changes
    /// </summary>
    private async Task ExecuteCompensatingTransactionsAsync(
        string? userId,
        bool userWasCreated,
        string? createdOrgId,
        bool userLinkedToOrg,
        bool databaseTransactionStarted)
    {
        // Compensate Step 4: Rollback database transaction
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

        // Compensate Step 3: Remove user from organisation
        if (userLinkedToOrg && userId != null && createdOrgId != null)
        {
            try
            {
                _logger.LogWarning("Compensating transaction: Removing user {UserId} from organisation {OrgId}", userId, createdOrgId);
                await _kcOrganisationService.RemoveUserFromOrganisationAsync(userId, createdOrgId);
                _logger.LogInformation("User removed from organisation successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove user {UserId} from organisation {OrgId}", userId, createdOrgId);
            }
        }

        // Compensate Step 2: Delete organisation
        if (createdOrgId != null)
        {
            try
            {
                _logger.LogWarning("Compensating transaction: Deleting organisation {OrgId}", createdOrgId);
                await _kcOrganisationService.DeleteOrganisationAsync(createdOrgId);
                _logger.LogInformation("Organisation deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete organisation {OrgId}", createdOrgId);
            }
        }

        // Compensate Step 1: Delete user (only if we created it in this saga)
        if (userWasCreated && userId != null)
        {
            try
            {
                _logger.LogWarning("Compensating transaction: Deleting user {UserId}", userId);
                await _kcUserService.DeleteUserAsync(userId);
                _logger.LogInformation("User deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete user {UserId}", userId);
            }
        }
    }

    private async Task AddUserAndTenantAsync(UserRepresentation userRepresentation, OrganizationRepresentation organizationRepresentation)
    {
        // Look up the pre-seeded Keycloak identity provider
        var keycloakProvider = await _unitOfWork.IdentityProviders.GetByNameAsync("Keycloak");
        if (keycloakProvider == null)
        {
            throw new InvalidOperationException("Keycloak identity provider not found in database. Ensure it is pre-seeded.");
        }

        // Generate internal GUIDs for user and tenant (NOT using Keycloak GUIDs)
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

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
            Email = userRepresentation.Email
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

        // Create tenant with internal GUID
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = organizationRepresentation.Name,
            Domains = organizationRepresentation.Domains
                .Select(d => new TenantDomain
                {
                    Domain = d.Name,
                    IsPrimary = d == organizationRepresentation.Domains.First()
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
            ExternalIdentifier = organizationRepresentation.Id
        };
        await _unitOfWork.ExternalIdentities.AddAsync(tenantExternalIdentity);

        // Get org-admin role for first user (they created the organization)
        var orgAdminRole = await _roleService.GetRoleByNameAsync("org-admin");
        if (orgAdminRole == null)
        {
            throw new InvalidOperationException("org-admin role not found in database. Ensure roles are pre-seeded.");
        }

        // Create TenantUser relationship (many-to-many join) with org-admin role
        var tenantUser = new TenantUser
        {
            TenantId = tenantId,
            UserId = userId,
            TenantUserRoles = new List<TenantUserRole> // First user should be org-admin
            {
                new TenantUserRole
                {
                    RoleId = orgAdminRole.Id
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