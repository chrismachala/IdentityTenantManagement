using System.Text.Json;
using IdentityTenantManagement.Helpers;
using IdentityTenantManagement.Helpers.ContentBuilders;
using IdentityTenantManagement.Models.Keycloak;
using IdentityTenantManagement.Models.Organisations;
using IO.Swagger.Model;
using Microsoft.Extensions.Options;

namespace IdentityTenantManagement.Services.KeycloakServices;

public interface IKCOrganisationService
{
    Task CreateOrgAsync(CreateTenantModel model);
    Task<OrganizationRepresentation> GetOrganisationByDomain(string domain);
    Task AddUserToOrganisationAsync(UserTenantModel model);
    Task InviteUserToOrganisationAsync(InviteUserModel model);
}

public class KCOrganisationService(IOptions<KeycloakConfig> config, IKCRequestHelper requestHelper) : IKCOrganisationService
{
    private readonly KeycloakConfig _config = config.Value;

    public async Task AddUserToOrganisationAsync(UserTenantModel userTenantModel)
    {
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations/{userTenantModel.TenantId}/members/invite-existing-user";  
        var request = requestHelper.CreateHttpRequestMessage(HttpMethod.Post, endpoint, new {id = userTenantModel.UserId}, new FormUrlEncodedContentBuilder());

        var response = await requestHelper.SendAsync(await request);
 
        response.EnsureSuccessStatusCode();
    }
    
    public async Task<OrganizationRepresentation> GetOrganisationByDomain(string domain)
    {
        
        string query = HttpQueryCreator.BuildQueryForTenantSearchByDomain(search:domain);
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations{query}";  
        var request = requestHelper.CreateHttpRequestMessage(HttpMethod.Get, endpoint, null, new JsonContentBuilder()); 
        var response = await requestHelper.SendAsync(await request);
 
        response.EnsureSuccessStatusCode();
        
        string json = await response.Content.ReadAsStringAsync();
        List<OrganizationRepresentation>? orgs = JsonSerializer.Deserialize<List<OrganizationRepresentation>>(json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        return orgs.First()??null;
    }
     
    public async Task CreateOrgAsync(CreateTenantModel orgModel)
    {
        var organisationsEndpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations";
        var org = new OrganizationRepresentation
        {
            Name = orgModel.Name,
            Enabled = true,
            Domains = new List<OrganizationDomainRepresentation>
            {
                new OrganizationDomainRepresentation { Name = orgModel.Domain }
            }
        };
        var request = requestHelper.CreateHttpRequestMessage(HttpMethod.Post, organisationsEndpoint, org, new JsonContentBuilder() );
        var response = await requestHelper.SendAsync(await request);

        response.EnsureSuccessStatusCode();

    }

    public async Task InviteUserToOrganisationAsync(InviteUserModel model)
    {
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations/{model.TenantId}/members/invite-user";

        var inviteData = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(model.Email))
            inviteData.Add("email", model.Email);

        if (!string.IsNullOrEmpty(model.FirstName))
            inviteData.Add("firstName", model.FirstName);

        if (!string.IsNullOrEmpty(model.LastName))
            inviteData.Add("lastName", model.LastName);

        var request = requestHelper.CreateHttpRequestMessage(HttpMethod.Post, endpoint, inviteData, new FormUrlEncodedContentBuilder());
        var response = await requestHelper.SendAsync(await request);

        response.EnsureSuccessStatusCode();
    }
}