using Asp.Versioning;
using IdentityTenantManagement.Services;
using KeycloakAdapter.Models;
using KeycloakAdapter.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IdentityTenantManagement.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class AuthenticationController : ControllerBase
{
    private readonly IAuthenticationOrchestrationService _authenticationService;
    private readonly PermissionService _permissionService;
    private readonly ILogger<AuthenticationController> _logger;

    public AuthenticationController(
        IAuthenticationOrchestrationService authenticationService,
        PermissionService permissionService,
        ILogger<AuthenticationController> logger)
    {
        _authenticationService = authenticationService;
        _permissionService = permissionService;
        _logger = logger;
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthenticationResponse>> Login([FromBody] LoginModel loginModel)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Only log organization, not username for privacy
        _logger.LogInformation("Login attempt for organization {Organization}", loginModel.Organization);

        var result = await _authenticationService.AuthenticateAsync(loginModel);

        if (!result.Success)
        {
            return Unauthorized(result);
        }

        return Ok(result);
    }

    [HttpGet("permissions/{userId}/{tenantId}")]
    public async Task<ActionResult<List<string>>> GetUserPermissions(Guid userId, Guid tenantId)
    {
        var permissions = await _permissionService.GetUserPermissionsAsync(tenantId, userId);
        return Ok(permissions);
    }
}