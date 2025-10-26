using IdentityTenantManagementDatabase.DbContexts;
using KeycloakAdapter.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagement.Controllers;

/// <summary>
/// Development-only controller for cleaning up test data.
/// IMPORTANT: This controller should ONLY be available in Development environment.
/// </summary>
[ApiController]
[Route("api/dev/[controller]")]
public class DevCleanupController : ControllerBase
{
    private readonly IdentityTenantManagementContext _dbContext;
    private readonly IKCOrganisationService _orgService;
    private readonly IKCUserService _userService;
    private readonly ILogger<DevCleanupController> _logger;
    private readonly IWebHostEnvironment _environment;

    public DevCleanupController(
        IdentityTenantManagementContext dbContext,
        IKCOrganisationService orgService,
        IKCUserService userService,
        ILogger<DevCleanupController> logger,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _orgService = orgService;
        _userService = userService;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Wipes ALL data from both the database and Keycloak.
    /// This includes all users, organizations, and their relationships.
    /// </summary>
    /// <returns>Summary of deleted items</returns>
    [HttpPost("wipe-all")]
    public async Task<IActionResult> WipeAllData()
    {
        // CRITICAL: Only allow in Development environment
        if (!_environment.IsDevelopment())
        {
            _logger.LogWarning("Attempted to wipe data in non-development environment!");
            return StatusCode(403, new { error = "This endpoint is only available in Development environment" });
        }

        _logger.LogWarning("=== STARTING DATA WIPE ===");

        var dbRecords = new Dictionary<string, int>
        {
            { "UserPermissions", 0 },
            { "TenantUsers", 0 },
            { "ExternalIdentities", 0 },
            { "RegistrationFailureLogs", 0 },
            { "Users", 0 },
            { "TenantDomains", 0 },
            { "Tenants", 0 }
        };

        var kcRecords = new Dictionary<string, int>
        {
            { "Organizations", 0 },
            { "Users", 0 }
        };

        var errors = new List<string>();

        try
        {
            // ============================================
            // STEP 1: Delete from Keycloak First
            // ============================================
            _logger.LogInformation("Step 1: Deleting data from Keycloak...");

            // Delete all organizations in Keycloak
            var kcOrganizations = await _orgService.GetAllOrganisationsAsync();
            _logger.LogInformation("Found {Count} organizations in Keycloak to delete", kcOrganizations.Count);

            foreach (var org in kcOrganizations)
            {
                try
                {
                    await _orgService.DeleteOrganisationAsync(org.Id);
                    kcRecords["Organizations"]++;
                    _logger.LogInformation("Deleted Keycloak organization: {OrgId} ({OrgName})", org.Id, org.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete Keycloak organization: {OrgId}", org.Id);
                    errors.Add($"Keycloak Org {org.Id}: {ex.Message}");
                }
            }

            // Delete all users in Keycloak
            var kcUsers = await _userService.GetAllUsersAsync();
            _logger.LogInformation("Found {Count} users in Keycloak to delete", kcUsers.Count);

            foreach (var user in kcUsers)
            {
                try
                {
                    await _userService.DeleteUserAsync(user.Id);
                    kcRecords["Users"]++;
                    _logger.LogInformation("Deleted Keycloak user: {UserId} ({Email})", user.Id, user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete Keycloak user: {UserId}", user.Id);
                    errors.Add($"Keycloak User {user.Id}: {ex.Message}");
                }
            }

            // ============================================
            // STEP 2: Delete from Database
            // ============================================
            _logger.LogInformation("Step 2: Deleting data from database...");

            // Use explicit transaction for database operations
            using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                // Delete in order to respect foreign key constraints

                // 1. Delete UserPermissions (depends on TenantUsers)
                var userPermissions = await _dbContext.UserPermissions.ToListAsync();
                _dbContext.UserPermissions.RemoveRange(userPermissions);
                dbRecords["UserPermissions"] = userPermissions.Count;
                _logger.LogInformation("Marked {Count} UserPermissions for deletion", userPermissions.Count);

                // 2. Delete TenantUsers (junction table, depends on Users and Tenants)
                var tenantUsers = await _dbContext.TenantUsers.ToListAsync();
                _dbContext.TenantUsers.RemoveRange(tenantUsers);
                dbRecords["TenantUsers"] = tenantUsers.Count;
                _logger.LogInformation("Marked {Count} TenantUsers for deletion", tenantUsers.Count);

                // 3. Delete ExternalIdentities (depends on Users and Tenants)
                var externalIdentities = await _dbContext.ExternalIdentities.ToListAsync();
                _dbContext.ExternalIdentities.RemoveRange(externalIdentities);
                dbRecords["ExternalIdentities"] = externalIdentities.Count;
                _logger.LogInformation("Marked {Count} ExternalIdentities for deletion", externalIdentities.Count);

                // 4. Delete RegistrationFailureLogs (independent table)
                var registrationFailureLogs = await _dbContext.RegistrationFailureLogs.ToListAsync();
                _dbContext.RegistrationFailureLogs.RemoveRange(registrationFailureLogs);
                dbRecords["RegistrationFailureLogs"] = registrationFailureLogs.Count;
                _logger.LogInformation("Marked {Count} RegistrationFailureLogs for deletion", registrationFailureLogs.Count);

                // 5. Delete Users
                var users = await _dbContext.Users.ToListAsync();
                _dbContext.Users.RemoveRange(users);
                dbRecords["Users"] = users.Count;
                _logger.LogInformation("Marked {Count} Users for deletion", users.Count);

                // 6. Delete TenantDomains (if separate table - check your DbContext)
                // Note: TenantDomain is referenced in OnModelCreating but not in DbSet
                // If it exists, uncomment:
                // var tenantDomains = await _dbContext.Set<TenantDomain>().ToListAsync();
                // _dbContext.Set<TenantDomain>().RemoveRange(tenantDomains);
                // dbRecords["TenantDomains"] = tenantDomains.Count;

                // 7. Delete Tenants
                var tenants = await _dbContext.Tenants.ToListAsync();
                _dbContext.Tenants.RemoveRange(tenants);
                dbRecords["Tenants"] = tenants.Count;
                _logger.LogInformation("Marked {Count} Tenants for deletion", tenants.Count);

                // Save all database changes
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                _logger.LogInformation("Database changes committed successfully");
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Error during database deletion, rolling back transaction");
                await transaction.RollbackAsync();
                throw;
            }

            _logger.LogWarning("=== DATA WIPE COMPLETED ===");

            return Ok(new
            {
                success = true,
                message = "All data has been wiped successfully",
                summary = new
                {
                    DatabaseRecordsDeleted = dbRecords,
                    KeycloakRecordsDeleted = kcRecords,
                    Errors = errors
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during data wipe");
            return StatusCode(500, new
            {
                success = false,
                error = "Critical error during data wipe",
                message = ex.Message,
                partialSummary = new
                {
                    DatabaseRecordsDeleted = dbRecords,
                    KeycloakRecordsDeleted = kcRecords,
                    Errors = errors
                }
            });
        }
    }

    /// <summary>
    /// Wipes only database data, leaving Keycloak untouched.
    /// Useful when you want to resync data.
    /// </summary>
    [HttpPost("wipe-database-only")]
    public async Task<IActionResult> WipeDatabaseOnly()
    {
        if (!_environment.IsDevelopment())
        {
            return StatusCode(403, new { error = "This endpoint is only available in Development environment" });
        }

        _logger.LogWarning("=== WIPING DATABASE ONLY ===");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            // Delete in proper order to respect FK constraints
            var userPermissions = await _dbContext.UserPermissions.ToListAsync();
            _dbContext.UserPermissions.RemoveRange(userPermissions);

            var tenantUsers = await _dbContext.TenantUsers.ToListAsync();
            _dbContext.TenantUsers.RemoveRange(tenantUsers);

            var externalIdentities = await _dbContext.ExternalIdentities.ToListAsync();
            _dbContext.ExternalIdentities.RemoveRange(externalIdentities);

            var registrationFailureLogs = await _dbContext.RegistrationFailureLogs.ToListAsync();
            _dbContext.RegistrationFailureLogs.RemoveRange(registrationFailureLogs);

            var users = await _dbContext.Users.ToListAsync();
            _dbContext.Users.RemoveRange(users);

            var tenants = await _dbContext.Tenants.ToListAsync();
            _dbContext.Tenants.RemoveRange(tenants);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Database wipe completed successfully");

            return Ok(new
            {
                success = true,
                message = "Database wiped successfully",
                deletedRecords = new
                {
                    UserPermissions = userPermissions.Count,
                    TenantUsers = tenantUsers.Count,
                    ExternalIdentities = externalIdentities.Count,
                    RegistrationFailureLogs = registrationFailureLogs.Count,
                    Users = users.Count,
                    Tenants = tenants.Count
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error wiping database, rolling back transaction");
            await transaction.RollbackAsync();
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Wipes only Keycloak data, leaving database untouched.
    /// Useful when you want to resync Keycloak.
    /// </summary>
    [HttpPost("wipe-keycloak-only")]
    public async Task<IActionResult> WipeKeycloakOnly()
    {
        if (!_environment.IsDevelopment())
        {
            return StatusCode(403, new { error = "This endpoint is only available in Development environment" });
        }

        _logger.LogWarning("=== WIPING KEYCLOAK ONLY ===");

        try
        {
            var orgCount = 0;
            var userCount = 0;
            var errors = new List<string>();

            // Delete organizations
            var kcOrganizations = await _orgService.GetAllOrganisationsAsync();
            foreach (var org in kcOrganizations)
            {
                try
                {
                    await _orgService.DeleteOrganisationAsync(org.Id);
                    orgCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Org {org.Id}: {ex.Message}");
                }
            }

            // Delete users
            var kcUsers = await _userService.GetAllUsersAsync();
            foreach (var user in kcUsers)
            {
                try
                {
                    await _userService.DeleteUserAsync(user.Id);
                    userCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"User {user.Id}: {ex.Message}");
                }
            }

            return Ok(new
            {
                success = true,
                message = "Keycloak wiped successfully",
                deletedRecords = new
                {
                    Organizations = orgCount,
                    Users = userCount
                },
                errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error wiping Keycloak");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets a count of all data without deleting anything.
    /// Useful to see what would be deleted.
    /// </summary>
    [HttpGet("data-summary")]
    public async Task<IActionResult> GetDataSummary()
    {
        if (!_environment.IsDevelopment())
        {
            return StatusCode(403, new { error = "This endpoint is only available in Development environment" });
        }

        try
        {
            var kcOrgs = await _orgService.GetAllOrganisationsAsync();
            var kcUsers = await _userService.GetAllUsersAsync();

            return Ok(new
            {
                database = new
                {
                    UserPermissions = await _dbContext.UserPermissions.CountAsync(),
                    TenantUsers = await _dbContext.TenantUsers.CountAsync(),
                    ExternalIdentities = await _dbContext.ExternalIdentities.CountAsync(),
                    RegistrationFailureLogs = await _dbContext.RegistrationFailureLogs.CountAsync(),
                    Users = await _dbContext.Users.CountAsync(),
                    Tenants = await _dbContext.Tenants.CountAsync()
                },
                keycloak = new
                {
                    Organizations = kcOrgs.Count,
                    Users = kcUsers.Count
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting data summary");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}