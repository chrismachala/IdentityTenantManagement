using IdentityTenantManagement.Models.Onboarding;
using IdentityTenantManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace IdentityTenantManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OnboardingController : ControllerBase
{
    private readonly IOnboardingService _onboardingService;

    public OnboardingController(IOnboardingService onboardingService)
    {
        _onboardingService = onboardingService;
    }

    [HttpPost("OnboardOrganisation")]
    public async Task<IActionResult> OnboardOrganisation([FromBody] TenantUserOnboardingModel body)
    {
        await _onboardingService.OnboardOrganisationAsync(body);

        return Ok(new { message = "Client Onboarded successfully" });
    }
}