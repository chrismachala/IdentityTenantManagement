using IdentityTenantManagement.Models.Organisations;
using IdentityTenantManagement.Models.Users;
using IdentityTenantManagement.Services.KeycloakServices;
using Microsoft.AspNetCore.Mvc;

namespace IdentityTenantManagement.Controllers;


[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IKCUserService _kcUserService;

    public UsersController(IKCUserService kcUserService)
    {
        _kcUserService = kcUserService;
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
}
