using System.ComponentModel.DataAnnotations;

namespace IdentityTenantManagement.Models.Organisations;

public class CreateTenantModel
{
    [Required]
    public string Name { get; set; }
    [Required]
    public string Domain { get; set; }
    
}