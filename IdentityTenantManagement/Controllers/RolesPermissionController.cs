using Asp.Versioning;
using IdentityTenantManagement.Models.Roles;
using IdentityTenantManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace IdentityTenantManagement.Controllers;

/// <summary>
/// Controller for managing user roles and permissions within tenants
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tenants/{tenantId}/[controller]")]
public class RolesPermissionController : ControllerBase
{
    private readonly IUserRoleService _userRoleService;
    private readonly ILogger<RolesPermissionController> _logger;

    public RolesPermissionController(
        IUserRoleService userRoleService,
        ILogger<RolesPermissionController> logger)
    {
        _userRoleService = userRoleService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all available roles that can be assigned to users
    /// </summary>
    /// <returns>List of available roles</returns>
    [HttpGet("roles")]
    [ProducesResponseType(typeof(List<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAvailableRoles()
    {
        try
        {
            var roles = await _userRoleService.GetAvailableRolesAsync();
            return Ok(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available roles");
            return StatusCode(500, new { message = "Failed to get available roles", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets a role with all its assigned permissions
    /// </summary>
    /// <param name="roleId">The role ID</param>
    /// <returns>Role with permissions</returns>
    [HttpGet("roles/{roleId}")]
    [ProducesResponseType(typeof(RoleWithPermissionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRoleWithPermissions(Guid roleId)
    {
        try
        {
            var role = await _userRoleService.GetRoleWithPermissionsAsync(roleId);
            return Ok(role);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Role {RoleId} not found", roleId);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role {RoleId} with permissions", roleId);
            return StatusCode(500, new { message = "Failed to get role with permissions", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets all available permissions in the system
    /// </summary>
    /// <returns>List of all permissions</returns>
    [HttpGet("permissions")]
    [ProducesResponseType(typeof(List<PermissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllPermissions()
    {
        try
        {
            var permissions = await _userRoleService.GetAllPermissionsAsync();
            return Ok(permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all permissions");
            return StatusCode(500, new { message = "Failed to get permissions", error = ex.Message });
        }
    }

    /// <summary>
    /// Creates a new role with optional permissions
    /// </summary>
    /// <param name="tenantId">The tenant ID (for API routing consistency)</param>
    /// <param name="request">The role creation request</param>
    /// <returns>Created role ID</returns>
    [HttpPost("roles")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateRole(Guid tenantId, [FromBody] CreateRoleRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var actorUserId = User.FindFirst("sub")?.Value ?? User.FindFirst("user_id")?.Value;
            var roleId = await _userRoleService.CreateRoleAsync(request, actorUserId);
            return CreatedAtAction(nameof(GetRoleWithPermissions), new { tenantId, roleId }, new { roleId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create role {RoleName}", request.Name);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role {RoleName}", request.Name);
            return StatusCode(500, new { message = "Failed to create role", error = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing role's basic information
    /// </summary>
    /// <param name="tenantId">The tenant ID (for API routing consistency)</param>
    /// <param name="roleId">The role ID to update</param>
    /// <param name="request">The role update request</param>
    /// <returns>Success message</returns>
    [HttpPut("roles/{roleId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateRole(Guid tenantId, Guid roleId, [FromBody] UpdateRoleRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var actorUserId = User.FindFirst("sub")?.Value ?? User.FindFirst("user_id")?.Value;
            await _userRoleService.UpdateRoleAsync(roleId, request, actorUserId);
            return Ok(new { message = "Role updated successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to update role {RoleId}", roleId);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role {RoleId}", roleId);
            return StatusCode(500, new { message = "Failed to update role", error = ex.Message });
        }
    }

    /// <summary>
    /// Assigns a permission to a role
    /// </summary>
    /// <param name="tenantId">The tenant ID (for API routing consistency)</param>
    /// <param name="roleId">The role ID</param>
    /// <param name="permissionId">The permission ID to assign</param>
    /// <returns>Success message</returns>
    [HttpPost("roles/{roleId}/permissions/{permissionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AssignPermissionToRole(Guid tenantId, Guid roleId, Guid permissionId)
    {
        try
        {
            var actorUserId = User.FindFirst("sub")?.Value ?? User.FindFirst("user_id")?.Value;
            await _userRoleService.AssignPermissionToRoleAsync(roleId, permissionId, actorUserId);
            return Ok(new { message = "Permission assigned successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to assign permission {PermissionId} to role {RoleId}", permissionId, roleId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning permission {PermissionId} to role {RoleId}", permissionId, roleId);
            return StatusCode(500, new { message = "Failed to assign permission", error = ex.Message });
        }
    }

    /// <summary>
    /// Removes a permission from a role
    /// </summary>
    /// <param name="tenantId">The tenant ID (for API routing consistency)</param>
    /// <param name="roleId">The role ID</param>
    /// <param name="permissionId">The permission ID to remove</param>
    /// <returns>Success message</returns>
    [HttpDelete("roles/{roleId}/permissions/{permissionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemovePermissionFromRole(Guid tenantId, Guid roleId, Guid permissionId)
    {
        try
        {
            var actorUserId = User.FindFirst("sub")?.Value ?? User.FindFirst("user_id")?.Value;
            await _userRoleService.RemovePermissionFromRoleAsync(roleId, permissionId, actorUserId);
            return Ok(new { message = "Permission removed successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to remove permission {PermissionId} from role {RoleId}", permissionId, roleId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing permission {PermissionId} from role {RoleId}", permissionId, roleId);
            return StatusCode(500, new { message = "Failed to remove permission", error = ex.Message });
        }
    }

    /// <summary>
    /// Gets all roles assigned to a specific user in the tenant
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>User roles with details</returns>
    [HttpGet("users/{userId}/roles")]
    [ProducesResponseType(typeof(UserRolesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserRoles(Guid tenantId, Guid userId)
    {
        try
        {
            var userRoles = await _userRoleService.GetUserRolesAsync(tenantId, userId);
            return Ok(userRoles);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "User {UserId} not found in tenant {TenantId}", userId, tenantId);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting roles for user {UserId} in tenant {TenantId}", userId, tenantId);
            return StatusCode(500, new { message = "Failed to get user roles", error = ex.Message });
        }
    }

    /// <summary>
    /// Assigns a role to a user in the tenant
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="request">The role assignment request containing userId and roleId</param>
    /// <returns>Success message</returns>
    [HttpPost("assign")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AssignRole(Guid tenantId, [FromBody] AssignRoleRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            // Get the calling user ID from claims (if available)
            var actorUserId = User.FindFirst("sub")?.Value ?? User.FindFirst("user_id")?.Value;

            await _userRoleService.AssignRoleToUserAsync(tenantId, request.UserId, request.RoleId, actorUserId);
            return Ok(new { message = "Role assigned successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to assign role {RoleId} to user {UserId} in tenant {TenantId}",
                request.RoleId, request.UserId, tenantId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role {RoleId} to user {UserId} in tenant {TenantId}",
                request.RoleId, request.UserId, tenantId);
            return StatusCode(500, new { message = "Failed to assign role", error = ex.Message });
        }
    }

    /// <summary>
    /// Removes a role from a user in the tenant
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="roleId">The role ID to remove</param>
    /// <returns>Success message</returns>
    [HttpDelete("users/{userId}/roles/{roleId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveRole(Guid tenantId, Guid userId, Guid roleId)
    {
        try
        {
            // Get the calling user ID from claims (if available)
            var actorUserId = User.FindFirst("sub")?.Value ?? User.FindFirst("user_id")?.Value;

            await _userRoleService.RemoveRoleFromUserAsync(tenantId, userId, roleId, actorUserId);
            return Ok(new { message = "Role removed successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to remove role {RoleId} from user {UserId} in tenant {TenantId}",
                roleId, userId, tenantId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing role {RoleId} from user {UserId} in tenant {TenantId}",
                roleId, userId, tenantId);
            return StatusCode(500, new { message = "Failed to remove role", error = ex.Message });
        }
    }

    /// <summary>
    /// Assigns a direct permission to a user
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="permissionId">The permission ID to assign</param>
    /// <returns>Success message</returns>
    [HttpPost("users/{userId}/permissions/{permissionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AssignPermissionToUser(Guid tenantId, Guid userId, Guid permissionId)
    {
        try
        {
            var actorUserId = User.FindFirst("sub")?.Value ?? User.FindFirst("user_id")?.Value;
            await _userRoleService.AssignPermissionToUserAsync(tenantId, userId, permissionId, actorUserId);
            return Ok(new { message = "Permission assigned successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to assign permission {PermissionId} to user {UserId} in tenant {TenantId}",
                permissionId, userId, tenantId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning permission {PermissionId} to user {UserId} in tenant {TenantId}",
                permissionId, userId, tenantId);
            return StatusCode(500, new { message = "Failed to assign permission", error = ex.Message });
        }
    }

    /// <summary>
    /// Removes a direct permission from a user
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="permissionId">The permission ID to remove</param>
    /// <returns>Success message</returns>
    [HttpDelete("users/{userId}/permissions/{permissionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemovePermissionFromUser(Guid tenantId, Guid userId, Guid permissionId)
    {
        try
        {
            var actorUserId = User.FindFirst("sub")?.Value ?? User.FindFirst("user_id")?.Value;
            await _userRoleService.RemovePermissionFromUserAsync(tenantId, userId, permissionId, actorUserId);
            return Ok(new { message = "Permission removed successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to remove permission {PermissionId} from user {UserId} in tenant {TenantId}",
                permissionId, userId, tenantId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing permission {PermissionId} from user {UserId} in tenant {TenantId}",
                permissionId, userId, tenantId);
            return StatusCode(500, new { message = "Failed to remove permission", error = ex.Message });
        }
    }
}