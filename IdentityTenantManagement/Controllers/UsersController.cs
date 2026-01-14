using Asp.Versioning;
using IdentityTenantManagement.Authorization;
using IdentityTenantManagement.Services;
using KeycloakAdapter.Models;
using KeycloakAdapter.Services;
using Microsoft.AspNetCore.Mvc;

namespace IdentityTenantManagement.Controllers;


[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserOrchestrationService _userOrchestrationService;
    private readonly IKCUserService _kcUserService;
    private readonly IKCOrganisationService _kcOrganisationService;
    private readonly IUserService _userService;
    private readonly IKCEventsService _kcEventsService;

    public UsersController(
        IUserOrchestrationService userOrchestrationService,
        IKCUserService kcUserService,
        IKCOrganisationService kcOrganisationService,
        IUserService userService,
        IKCEventsService kcEventsService)
    {
        _userOrchestrationService = userOrchestrationService;
        _kcUserService = kcUserService;
        _kcOrganisationService = kcOrganisationService;
        _userService = userService;
        _kcEventsService = kcEventsService;
    }

    [HttpGet("organization/{organizationId}")]
    public async Task<IActionResult> GetOrganizationUsers(string organizationId)
    {
        var users = await _kcOrganisationService.GetOrganisationUsersAsync(organizationId);
        return Ok(users);
    }

    [HttpPost("Create")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserModel body, [FromQuery] string? organizationId = null)
    {
        var userId = await _userOrchestrationService.CreateUserAsync(body, organizationId);
        return Ok(new {message="User created successfully", emailAddress=body.Email, userId = userId });
    }

    [HttpPost("GetUserByEmail")]
    public async Task<IActionResult> GetUserByEmail([FromBody] string body)
    {
        await _kcUserService.GetUserByEmailAsync(body);
        return Ok(new {message="User Found", emailAddress=body });
    }

    [HttpPut("{userId}")]
    public async Task<IActionResult> UpdateUser(string userId, [FromBody] EditUserModel body)
    {
        var tenantId = User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantId))
        {
            return Unauthorized(new { message = "Tenant context not found" });
        }

        await _userService.UpdateUserAsync(userId, body, tenantId);
        return Ok(new {message="User updated successfully"});
    }

    // [HttpDelete("{userId}")]
    // [RequirePermission("delete-users")]
    // [Obsolete("Use DELETE /tenants/{tenantId}/users/{userId} instead. This endpoint is deprecated and will be removed in a future version.")]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        // Legacy endpoint - redirects to new soft-delete logic
        try
        {
            var tenantId = User.FindFirst("tenant_id")?.Value;

            if (string.IsNullOrEmpty(tenantId))
            {
                return Unauthorized(new { message = "Tenant context not found" });
            }

            if (!Guid.TryParse(userId, out var userGuid) || !Guid.TryParse(tenantId, out var tenantGuid))
            {
                return BadRequest(new { message = "Invalid user ID or tenant ID format" });
            }

            await _userService.DeactivateUserInTenantAsync(tenantGuid, userGuid);
            return Ok(new { message = "User soft-deleted successfully. Use the reactivation endpoint to restore access." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to delete user", error = ex.Message });
        }
    }

    [HttpGet("events/recent-registrations")]
    public async Task<IActionResult> GetRecentRegistrations()
    {
        try
        {
            var registrations = await _kcEventsService.GetRecentRegistrationEventsAsync();
            return Ok(new
            {
                message = "Recent registration events retrieved successfully",
                count = registrations.Count,
                registrations = registrations
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Failed to retrieve registration events",
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }
}
