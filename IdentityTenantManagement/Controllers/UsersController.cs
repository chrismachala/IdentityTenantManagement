using IdentityTenantManagement.Services;
using KeycloakAdapter.Models;
using KeycloakAdapter.Services;
using Microsoft.AspNetCore.Mvc;

namespace IdentityTenantManagement.Controllers;


[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IKCUserService _kcUserService;
    private readonly IKCOrganisationService _kcOrganisationService;
    private readonly IUserService _userService;
    private readonly IKCEventsService _kcEventsService;

    public UsersController(
        IKCUserService kcUserService,
        IKCOrganisationService kcOrganisationService,
        IUserService userService,
        IKCEventsService kcEventsService)
    {
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
    public async Task<IActionResult> CreateUser([FromBody] CreateUserModel body)
    {
        await _kcUserService.CreateUserAsync(body);
        return Ok(new {message="User created successfully", emailAddress=body.Email });
    }

    [HttpPost("GetUserByEmail")]
    public async Task<IActionResult> GetUserByEmail([FromBody] string body)
    {
        await _kcUserService.GetUserByEmailAsync(body);
        return Ok(new {message="User Found", emailAddress=body });
    }

    [HttpPut("{userId}")]
    public async Task<IActionResult> UpdateUser(string userId, [FromBody] CreateUserModel body)
    {
        await _userService.UpdateUserAsync(userId, body);
        return Ok(new {message="User updated successfully"});
    }

    [HttpDelete("{userId}")]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        await _kcUserService.DeleteUserAsync(userId);
        return Ok(new {message="User deleted successfully"});
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
