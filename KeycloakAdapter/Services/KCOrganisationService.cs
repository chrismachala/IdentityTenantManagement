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
    Task InviteUserToOrganisationAsync(InviteUserModel model);
    Task DeleteOrganisationAsync(string orgId);
    Task RemoveUserFromOrganisationAsync(string userId, string orgId);
    Task<List<OrganizationRepresentation>> GetAllOrganisationsAsync();
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

        var endpoint = BuildEndpoint($"organizations/{userTenantModel.TenantId}/members/invite-existing-user");

        await PostFormAsync(endpoint, new { id = userTenantModel.UserId });

        Logger.LogInformation("Successfully added user {UserId} to organisation {OrgId}", userTenantModel.UserId, userTenantModel.TenantId);
    }

    public async Task InviteUserToOrganisationAsync(InviteUserModel model)
    {
        Logger.LogInformation("Inviting user {Email} to organisation {OrgId}", model.Email, model.TenantId);

        var endpoint = BuildEndpoint($"organizations/{model.TenantId}/members/invite-user");

        var inviteData = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(model.Email))
            inviteData.Add("email", model.Email);

        if (!string.IsNullOrEmpty(model.FirstName))
            inviteData.Add("firstName", model.FirstName);

        if (!string.IsNullOrEmpty(model.LastName))
            inviteData.Add("lastName", model.LastName);

        await PostFormAsync(endpoint, inviteData);

        Logger.LogInformation("Successfully invited user {Email} to organisation {OrgId}", model.Email, model.TenantId);
    }

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
}