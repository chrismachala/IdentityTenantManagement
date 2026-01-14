using KeycloakAdapter.Exceptions;
using KeycloakAdapter.Helpers;
using KeycloakAdapter.Models;
using IO.Swagger.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KeycloakAdapter.Services;

public interface IKCOrganisationService
{
    Task CreateOrgAsync(CreateTenantModel model);
    Task<OrganizationRepresentation> GetOrganisationByDomain(string domain);
    Task AddUserToOrganisationAsync(UserTenantModel model); 
    Task DeleteOrganisationAsync(string orgId);
    Task RemoveUserFromOrganisationAsync(string userId, string orgId);
    Task<List<OrganizationRepresentation>> GetAllOrganisationsAsync();
    Task<List<UserRepresentation>> GetOrganisationUsersAsync(string orgId);
}

public class KCOrganisationService : KeycloakServiceBase, IKCOrganisationService
{
    public KCOrganisationService(
        IOptions<KeycloakConfig> config,
        IKCRequestHelper requestHelper,
        ILogger<KCOrganisationService> logger)
        : base(config, requestHelper, logger)
    {
    }

    public async Task<OrganizationRepresentation> GetOrganisationByDomain(string domain)
    {
        Logger.LogInformation("Getting organisation by domain: {Domain}", domain);

        var query = HttpQueryCreator.BuildQueryForTenantSearchByDomain(search: domain);
        var endpoint = BuildEndpoint($"organizations{query}");

        var orgs = await GetAsync<List<OrganizationRepresentation>>(endpoint);

        return GetFirstOrThrow(orgs, "Organization", $"domain={domain}");
    }

    public async Task CreateOrgAsync(CreateTenantModel orgModel)
    {
        Logger.LogInformation("Creating organisation: {Name} with domain {Domain}", orgModel.Name, orgModel.Domain);

        var endpoint = BuildEndpoint("organizations");

        var org = new OrganizationRepresentation
        {
            Name = orgModel.Name,
            Enabled = true,
            Domains = new List<OrganizationDomainRepresentation>
            {
                new OrganizationDomainRepresentation { Name = orgModel.Domain }
            }
        };

        await PostJsonAsync(endpoint, org);
        Logger.LogInformation("Successfully created organisation: {Name}", orgModel.Name);
    }

    public async Task AddUserToOrganisationAsync(UserTenantModel userTenantModel)
    {
        Logger.LogInformation("Adding user {UserId} to organisation {OrgId}", userTenantModel.UserId, userTenantModel.TenantId);

        var endpoint = BuildEndpoint($"organizations/{userTenantModel.TenantId}/members");

        await PostJsonAsync(endpoint, userTenantModel.UserId );

        Logger.LogInformation("Successfully added user {UserId} to organisation {OrgId}", userTenantModel.UserId, userTenantModel.TenantId);
    }

    // public async Task<string> InviteUserToOrganisationAsync(InviteUserModel model)
    // {
    //     Logger.LogInformation("Inviting user {Email} to organisation {OrgId}", model.Email, model.TenantId);
    //
    //     var endpoint = BuildEndpoint($"organizations/{model.TenantId}/members/invite-user");
    //
    //     var inviteData = new Dictionary<string, string>();
    //
    //     if (!string.IsNullOrEmpty(model.Email))
    //         inviteData.Add("email", model.Email);
    //
    //     if (!string.IsNullOrEmpty(model.FirstName))
    //         inviteData.Add("firstName", model.FirstName);
    //
    //     if (!string.IsNullOrEmpty(model.LastName))
    //         inviteData.Add("lastName", model.LastName);
    //
    //     var response = await PostFormAsync(endpoint, inviteData);
    //
    //     // Extract user ID from Location header
    //     // Keycloak returns the user ID in the Location header: .../users/{userId}
    //     var locationHeader = response.Headers.Location?.ToString();
    //     if (string.IsNullOrEmpty(locationHeader))
    //     {
    //         throw new KeycloakException("Failed to get user ID from invite response - Location header not found", response.StatusCode, "");
    //     }
    //
    //     var userId = locationHeader.Split('/').Last();
    //
    //     Logger.LogInformation("Successfully invited user {Email} to organisation {OrgId}, user ID: {UserId}", model.Email, model.TenantId, userId);
    //
    //     return userId;
    // }

    public async Task DeleteOrganisationAsync(string orgId)
    {
        Logger.LogWarning("Deleting organisation: {OrgId}", orgId);

        var endpoint = BuildEndpoint($"organizations/{orgId}");
        await DeleteAsync(endpoint);

        Logger.LogInformation("Successfully deleted organisation: {OrgId}", orgId);
    }

    public async Task RemoveUserFromOrganisationAsync(string userId, string orgId)
    {
        Logger.LogWarning("Removing user {UserId} from organisation {OrgId}", userId, orgId);

        var endpoint = BuildEndpoint($"organizations/{orgId}/members/{userId}");
        await DeleteAsync(endpoint);

        Logger.LogInformation("Successfully removed user {UserId} from organisation {OrgId}", userId, orgId);
    }

    public async Task<List<OrganizationRepresentation>> GetAllOrganisationsAsync()
    {
        Logger.LogInformation("Getting all organisations");

        var endpoint = BuildEndpoint("organizations");
        var orgs = await GetAsync<List<OrganizationRepresentation>>(endpoint);

        Logger.LogInformation("Found {Count} organisations", orgs?.Count ?? 0);
        return orgs ?? new List<OrganizationRepresentation>();
    }

    public async Task<List<UserRepresentation>> GetOrganisationUsersAsync(string orgId)
    {
        Logger.LogInformation("Getting users for organisation {OrgId}", orgId);

        var endpoint = BuildEndpoint($"organizations/{orgId}/members");
        var users = await GetAsync<List<UserRepresentation>>(endpoint);

        Logger.LogInformation("Found {Count} users in organisation {OrgId}", users?.Count ?? 0, orgId);
        return users ?? new List<UserRepresentation>();
    }
}