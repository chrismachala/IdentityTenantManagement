using IdentityTenantManagement.Constants;
using IdentityTenantManagementDatabase.Models;
using IdentityTenantManagementDatabase.Repositories;
using IO.Swagger.Model;
using KeycloakAdapter.Models;
using KeycloakAdapter.Services;

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
    private readonly ILogger<TenantOrchestrationService> _logger;
    private readonly IRoleService _roleService;

    public TenantOrchestrationService(
        IKCOrganisationService kcOrganisationService,
        IUnitOfWork unitOfWork,
        ILogger<TenantOrchestrationService> logger,
        IRoleService roleService)
    {
        _kcOrganisationService = kcOrganisationService;
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
        string? invitedUserId = null;
        bool databaseTransactionStarted = false;

        try
        {
            // Step 1: Invite user in Keycloak (creates user and adds to org)
            _logger.LogInformation("Saga Step 1: Inviting user to tenant in Keycloak");
            invitedUserId = await _kcOrganisationService.InviteUserToOrganisationAsync(model);
            _logger.LogInformation("Saga Step 1: User invited successfully with ID {UserId}", invitedUserId);

            // Step 2: Persist user to database with transaction
            // Note: User details will be synced by RegistrationProcessorService
            // This ensures the user-tenant relationship is tracked
            _logger.LogInformation("Saga Step 2: Persisting user invitation to database");
            await _unitOfWork.BeginTransactionAsync();
            databaseTransactionStarted = true;

            await PersistInvitedUserToDatabaseAsync(invitedUserId, model);
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Saga completed successfully for user invitation {UserId} to tenant {TenantId}", invitedUserId, model.TenantId);
            return invitedUserId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga failed for user invitation to tenant {TenantId}. Starting compensating transactions. State: UserId={UserId}, DbTransaction={DbTransaction}",
                model.TenantId, invitedUserId, databaseTransactionStarted);

            // Execute compensating transactions in reverse order
            await ExecuteInviteUserCompensatingTransactionsAsync(invitedUserId, model.TenantId, databaseTransactionStarted);

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
        string? invitedUserId,
        string tenantId,
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

        // Compensate Step 1: Remove user from organization and delete from Keycloak
        if (invitedUserId != null)
        {
            try
            {
                _logger.LogWarning("Compensating transaction: Removing user {UserId} from tenant {TenantId}", invitedUserId, tenantId);
                await _kcOrganisationService.RemoveUserFromOrganisationAsync(invitedUserId, tenantId);
                _logger.LogInformation("User removed from tenant successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove user {UserId} from tenant {TenantId}", invitedUserId, tenantId);
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
    /// Persists invited user to database with tenant relationship
    /// </summary>
    private async Task PersistInvitedUserToDatabaseAsync(string keycloakUserId, InviteUserModel model)
    {
        // Look up the pre-seeded Keycloak identity provider
        var keycloakProvider = await _unitOfWork.IdentityProviders.GetByNameAsync("Keycloak");
        if (keycloakProvider == null)
        {
            throw new InvalidOperationException("Keycloak identity provider not found in database. Ensure it is pre-seeded.");
        }

        // Check if user already exists
        var existingUserExternalIdentity = await _unitOfWork.ExternalIdentities.GetByExternalIdentifierAsync(keycloakUserId, keycloakProvider.Id);
        if (existingUserExternalIdentity != null)
        {
            _logger.LogWarning("User with Keycloak ID {UserId} already exists in database. Skipping creation.", keycloakUserId);
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

        // Create user with internal GUID (basic info - full sync happens via RegistrationProcessorService)
        var user = new User
        {
            Id = userId,
            Email = model.Email ?? string.Empty,
            FirstName = model.FirstName ?? string.Empty,
            LastName = model.LastName ?? string.Empty
        };
        await _unitOfWork.Users.AddAsync(user);

        // Create ExternalIdentity for user
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

        // Create TenantUser relationship
        var tenantUser = new TenantUser
        {
            TenantId = tenantExternalIdentity.EntityId,
            UserId = userId,
            RoleId = orgUserRole.Id
        };
        await _unitOfWork.TenantUsers.AddAsync(tenantUser);
    }
}