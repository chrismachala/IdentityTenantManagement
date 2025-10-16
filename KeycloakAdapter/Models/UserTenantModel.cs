using System.ComponentModel.DataAnnotations;

namespace KeycloakAdapter.Models;

public class UserTenantModel
{
    [Required]
    public string UserId { get; set; }
    [Required]
    public string TenantId { get; set; }

}