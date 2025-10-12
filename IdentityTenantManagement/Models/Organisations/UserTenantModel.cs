using System.ComponentModel.DataAnnotations;

namespace IdentityTenantManagement.Models.Organisations;

public class UserTenantModel
{
    [Required]
    public string UserId { get; set; }
    [Required]
    public string TenantId { get; set; }
    
}