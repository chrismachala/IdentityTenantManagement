using System.ComponentModel.DataAnnotations;

namespace KeycloakAdapter.Models;

public class CreateTenantModel
{
    [Required]
    public string Name { get; set; }
    [Required]
    public string Domain { get; set; }

}