using System.Net.Http.Headers;
using System.Text.Json;
using IO.Swagger.Model;
using KeycloakAdapter.Exceptions;
using KeycloakAdapter.Helpers;
using KeycloakAdapter.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace KeycloakAdapter.Services;

public interface IKCAuthenticationService
{
    Task<AuthenticationResponse> AuthenticateAsync(LoginModel loginModel);
}

public class KCAuthenticationService : IKCAuthenticationService
{
    private readonly KeycloakConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<KCAuthenticationService> _logger;
    private readonly IKCRequestHelper _requestHelper;

    public KCAuthenticationService(
        IOptions<KeycloakConfig> config,
        IHttpClientFactory httpClientFactory,
        IKCRequestHelper requestHelper,
        ILogger<KCAuthenticationService> logger)
    {
        _config = config.Value;
        _httpClient = httpClientFactory.CreateClient();
        _requestHelper = requestHelper;
        _logger = logger;
    }

    public async Task<AuthenticationResponse> AuthenticateAsync(LoginModel loginModel)
    {
        try
        {
            // Only log organization for privacy
            _logger.LogInformation("Authentication attempt for organization {Organization}", loginModel.Organization);

            // Always authenticate against the "Organisations" realm
            const string realm = "Organisations";
            var tokenEndpoint = $"{_config.BaseUrl}/realms/{realm}/protocol/openid-connect/token";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("client_id", _config.ClientId),
                new KeyValuePair<string, string>("client_secret", _config.ClientSecret),
                new KeyValuePair<string, string>("username", loginModel.Username),
                new KeyValuePair<string, string>("password", loginModel.Password),
                new KeyValuePair<string, string>("scope", "openid profile email")
            });

            var response = await _httpClient.PostAsync(tokenEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                // Don't log error details or username for security
                _logger.LogWarning("Authentication failed for organization {Organization}", loginModel.Organization);

                return new AuthenticationResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid credentials"
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var tokenResponse = JObject.Parse(responseBody);

            var accessToken = tokenResponse["access_token"]?.ToString();
            var refreshToken = tokenResponse["refresh_token"]?.ToString();
            var expiresIn = tokenResponse["expires_in"]?.Value<int>() ?? 0;

            // Get user info and verify organization membership
            var userInfo = await GetUserInfoAsync(accessToken!, loginModel.Organization);

            if (userInfo == null)
            {
                _logger.LogWarning("User does not belong to organization {Organization}", loginModel.Organization);

                return new AuthenticationResponse
                {
                    Success = false,
                    ErrorMessage = "User does not have access to the specified organization"
                };
            }

            _logger.LogInformation("Successfully authenticated user for organization {Organization}", loginModel.Organization);

            return new AuthenticationResponse
            {
                Success = true,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = expiresIn,
                UserInfo = userInfo
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication");
            return new AuthenticationResponse
            {
                Success = false,
                ErrorMessage = "An error occurred during authentication"
            };
        }
    }

    private async Task<UserInfo?> GetUserInfoAsync(string accessToken, string organization)
    {
        try
        {
            const string realm = "Organisations";
            var userInfoEndpoint = $"{_config.BaseUrl}/realms/{realm}/protocol/openid-connect/userinfo";

            var request = new HttpRequestMessage(HttpMethod.Get, userInfoEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get user info");
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var userInfoJson = JObject.Parse(responseBody);

            var userId = userInfoJson["sub"]?.ToString() ?? string.Empty;

            // Get organization details
            var org = await GetOrganizationByNameAsync(organization);

            if (org == null)
            {
                _logger.LogWarning("Organization {Organization} not found", organization);
                return null;
            }

            // Verify user belongs to the specified organization
            var isMember = await VerifyOrganizationMembershipAsync(userId, organization);

            if (!isMember)
            {
                _logger.LogWarning("User {UserId} is not a member of organization {Organization}", userId, organization);
                return null;
            }

            return new UserInfo
            {
                UserId = userId,
                Username = userInfoJson["preferred_username"]?.ToString() ?? string.Empty,
                Email = userInfoJson["email"]?.ToString() ?? string.Empty,
                FirstName = userInfoJson["given_name"]?.ToString() ?? string.Empty,
                LastName = userInfoJson["family_name"]?.ToString() ?? string.Empty,
                Organization = organization,
                OrganizationId = org.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user info");
            return null;
        }
    }

    private async Task<bool> VerifyOrganizationMembershipAsync(string userId, string organizationName)
    {
        try
        {
            _logger.LogDebug("Verifying user {UserId} membership in organization {Organization}", userId, organizationName);

            const string realm = "Organisations";

            // First, get the organization by name to get its ID
            var org = await GetOrganizationByNameAsync(organizationName);

            if (org == null)
            {
                _logger.LogWarning("Organization {Organization} not found", organizationName);
                return false;
            }

            // Check if the user is a member of this organization
            var endpoint = $"{_config.BaseUrl}/admin/realms/{realm}/organizations/{org.Id}/members/{userId}";

            var adminRequest = await _requestHelper.CreateHttpRequestMessage(
                HttpMethod.Get,
                endpoint,
                null,
                new Helpers.ContentBuilders.JsonContentBuilder());

            var memberResponse = await _requestHelper.SendAsync(adminRequest);

            // If we get a successful response, the user is a member
            return true;
        }
        catch (KeycloakException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("User {UserId} is not a member of organization {Organization}", userId, organizationName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying organization membership for user {UserId}", userId);
            return false;
        }
    }

    private async Task<OrganizationRepresentation?> GetOrganizationByNameAsync(string organizationName)
    {
        try
        {
            const string realm = "Organisations";
            var query = HttpQueryCreator.BuildQueryForTenantSearchByName(organizationName);
            var endpoint = $"{_config.BaseUrl}/admin/realms/{realm}/organizations{query}";

            var adminRequest = await _requestHelper.CreateHttpRequestMessage(
                HttpMethod.Get,
                endpoint,
                null,
                new Helpers.ContentBuilders.JsonContentBuilder());

            var response = await _requestHelper.SendAsync(adminRequest);
            var json = await response.Content.ReadAsStringAsync();

            var orgs = JsonSerializer.Deserialize<List<OrganizationRepresentation>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return orgs?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting organization {Organization}", organizationName);
            return null;
        }
    }
}