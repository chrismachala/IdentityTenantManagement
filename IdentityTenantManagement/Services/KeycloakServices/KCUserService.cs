using System.Text.Json;
using IdentityTenantManagement.Helpers;
using IdentityTenantManagement.Helpers.ContentBuilders;
using IdentityTenantManagement.Models.Keycloak;
using IdentityTenantManagement.Models.Users;
using IO.Swagger.Model;
using Microsoft.Extensions.Options;

namespace IdentityTenantManagement.Services.KeycloakServices;

public interface IKCUserService
{
    Task CreateUserAsync(CreateUserModel model);
    Task<UserRepresentation> GetUserByEmailAsync(string email);
}

public class KCUserService(IOptions<KeycloakConfig> config, IKCRequestHelper requestHelper) : IKCUserService
{
    private readonly KeycloakConfig _config = config.Value;

    public async Task<UserRepresentation> GetUserByEmailAsync(string emailAddress)
    { 
        string query = HttpQueryCreator.BuildQueryForUserSearchByEmail(emailAddress);
        string endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/users{query}";
 
        Task<HttpRequestMessage> request = requestHelper.CreateHttpRequestMessage(HttpMethod.Get, endpoint,null, new JsonContentBuilder());
        HttpResponseMessage response = await requestHelper.SendAsync(await request);
        response.EnsureSuccessStatusCode();
        
        string json = await response.Content.ReadAsStringAsync();

        List<UserRepresentation>? users = JsonSerializer.Deserialize<List<UserRepresentation>>(json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return users.First()??null;
    }
     
    public async Task CreateUserAsync(CreateUserModel userModel)
    {
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/users";
        
        var user = new UserRepresentation
        {   
            Username = userModel.UserName,
            Email = userModel.Email,
            FirstName = userModel.FirstName,
            LastName = userModel.LastName,
            Enabled = true,
            EmailVerified = true,
            Credentials = new List<CredentialRepresentation>
            {
                new CredentialRepresentation
                {
                    Type = "password",
                    Value = userModel.Password,
                    Temporary = false
                }
            },
            RequiredActions = new List<string>()
        }; 
        var request = requestHelper.CreateHttpRequestMessage(HttpMethod.Post, endpoint, user, new JsonContentBuilder());
        var response = await requestHelper.SendAsync(await request);
      
        response.EnsureSuccessStatusCode();
  
    } 
}