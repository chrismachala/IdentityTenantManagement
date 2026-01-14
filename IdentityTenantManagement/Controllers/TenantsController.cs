using Asp.Versioning;
using IdentityTenantManagement.Models.Organisations;
using IdentityTenantManagement.Models.Users;
using IdentityTenantManagement.Services;
using Microsoft.AspNetCore.Mvc;
using KeycloakAdapter.Models;
using KeycloakAdapter.Services;


namespace IdentityTenantManagement.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class TenantsController : ControllerBase
{
    private readonly ITenantOrchestrationService _tenantOrchestrationService;
    private readonly IKCOrganisationService _kcOrganisationService;
    private readonly IUserService _userService;

    public TenantsController(
        ITenantOrchestrationService tenantOrchestrationService,
        IKCOrganisationService kcOrganisationService,
        IUserService userService)
    {
        _tenantOrchestrationService = tenantOrchestrationService;
        _kcOrganisationService = kcOrganisationService;
        _userService = userService;
    } 
    
    [HttpPost("Create")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantModel body)
    {
          var tenantId = await _tenantOrchestrationService.CreateTenantAsync(body);
          return Ok(new {message="Organisation created successfully", tenantName=body.Name, tenantId = tenantId });
    } 
    
    [HttpPost("GetTenantByDomain")]
    public async Task<IActionResult> GetTenantByDomain([FromBody] string body)
    {
        await _kcOrganisationService.GetOrganisationByDomain(body);
        return Ok(new {message="Organisation created successfully", tenantName=body }); 
    } 

    [HttpPost("InviteExistingUser")]
    public async Task<IActionResult> InviteExistingUser([FromBody] UserTenantModel body)
    {
        await _kcOrganisationService.AddUserToOrganisationAsync(body);
        return Ok(new {message="User successfully added to Organisation" });

    }

    [HttpPost("AddDomainToOrganisation")]
    public async Task<IActionResult> AddDomainToOrganisation([FromBody] TenantDomainModel body)
    {
        throw new NotImplementedException();
    }
    

    [HttpPost("InviteUser")]
    public async Task<IActionResult> InviteUser([FromBody] InviteUserModel body)
    {
        var userId = await _tenantOrchestrationService.InviteUserToTenantAsync(body);
        return Ok(new {message="User invitation sent successfully", userId = userId });
    }

    /// <summary>
    /// Gets a paginated list of users for a tenant with optional filtering of inactive users.
    /// </summary>
    /// <param name="tenantId">The internal tenant ID</param>
    /// <param name="includeInactive">Whether to include inactive users (default: false)</param>
    /// <param name="page">Page number (1-based, default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 50, max: 100)</param>
    /// <returns>Paginated list of users with metadata</returns>
    [HttpGet("{tenantId}/users")]
    public async Task<ActionResult<PaginatedUsersResponse>> GetTenantUsers(
        Guid tenantId,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100; // Cap at 100 for performance

            var (users, totalCount) = await _userService.GetUsersAsync(
                tenantId,
                includeInactive,
                page,
                pageSize);

            var response = new PaginatedUsersResponse
            {
                Users = users.ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to retrieve users", error = ex.Message });
        }
    }

    /// <summary>
    /// Soft-deletes a user from a specific tenant by marking their profile as inactive and revoking all sessions.
    /// This operation is reversible via the reactivation endpoint.
    /// </summary>
    /// <param name="tenantId">The tenant ID from which to remove the user</param>
    /// <param name="userId">The user ID to soft-delete</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown</param>
    /// <returns>204 No Content on success, 404 if user/tenant not found, 500 on error</returns>
    [HttpDelete("{tenantId}/users/{userId}")]
    public async Task<IActionResult> DeleteUserFromTenant(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            await _userService.DeactivateUserInTenantAsync(tenantId, userId, cancellationToken);
            return NoContent(); // 204 No Content - successful deletion with no response body
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to deactivate user", error = ex.Message });
        }
    }

    /// <summary>
    /// Reactivates a previously deactivated user in a specific tenant by marking their profile as active.
    /// Clears the inactive timestamp and global inactive state if user becomes active in at least one tenant.
    /// </summary>
    /// <param name="tenantId">The tenant ID in which to reactivate the user</param>
    /// <param name="userId">The user ID to reactivate</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown</param>
    /// <returns>204 No Content on success, 404 if user/tenant not found, 500 on error</returns>
    [HttpPut("{tenantId}/users/{userId}/reactivate")]
    public async Task<IActionResult> ReactivateUserInTenant(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            await _userService.ReactivateUserInTenantAsync(tenantId, userId, cancellationToken);
            return NoContent(); // 204 No Content - successful reactivation with no response body
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to reactivate user", error = ex.Message });
        }
    }
}