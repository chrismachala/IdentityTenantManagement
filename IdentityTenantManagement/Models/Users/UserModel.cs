using System.ComponentModel.DataAnnotations;

namespace IdentityTenantManagement.Models.Users;

public class CreateUserModel
{
    [Required]
    public string UserName { get; set; }
    [Required]
    public string FirstName { get; set; }
    [Required]
    public string LastName { get; set; }  
    [Required]
    public string Email { get; set; }
    [Required]
    public string Password { get; set; }
    
}