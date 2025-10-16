using System.ComponentModel.DataAnnotations;
using KeycloakAdapter.Models;

namespace IdentityTenantManagement.Models.Onboarding;

public class TenantUserOnboardingModel
{
    [Required]
    public CreateTenantModel CreateTenantModel { get; set; }
    [Required]
    public CreateUserModel CreateUserModel { get; set; }

}