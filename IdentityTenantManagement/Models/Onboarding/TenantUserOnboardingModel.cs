using System.ComponentModel.DataAnnotations;
using IdentityTenantManagement.Models.Organisations;
using IdentityTenantManagement.Models.Users;

namespace IdentityTenantManagement.Models.Onboarding;

public class TenantUserOnboardingModel
{
    [Required] 
    public CreateTenantModel CreateTenantModel { get; set; }
    [Required]
    public CreateUserModel CreateUserModel { get; set; } 
    
}