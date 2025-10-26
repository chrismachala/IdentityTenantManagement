using IdentityTenantManagement.Models.Organisations;
using IdentityTenantManagement.Services;
using Microsoft.AspNetCore.Mvc;
using KeycloakAdapter.Models;
using KeycloakAdapter.Services;


namespace IdentityTenantManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TenantsController : ControllerBase
{
    private readonly IKCOrganisationService _kcOrganisationService;
    private readonly IUserService _userService;

    public TenantsController(IKCOrganisationService kcOrganisationService, IUserService userService)
    {
        _kcOrganisationService = kcOrganisationService;
        _userService = userService;
    } 
    
    [HttpPost("Create")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantModel body)
    {
          await _kcOrganisationService.CreateOrgAsync(body);
          return Ok(new {message="Organisation created successfully", tenantName=body.Name }); 
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
        // Invite user in Keycloak
        await _kcOrganisationService.InviteUserToOrganisationAsync(body);

        return Ok(new {message="User invitation sent successfully" });
    }
}