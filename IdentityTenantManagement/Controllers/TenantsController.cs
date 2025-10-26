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
}