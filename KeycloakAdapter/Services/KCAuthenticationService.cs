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
    Task<UserInfo?> GetUserInfoForOrganizationAsync(string accessToken, string organizationId);
    Task<List<OrganizationInfo>> GetOrganizationsForAccessTokenAsync(string accessToken);
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
            _logger.LogInformation("Authentication attempt for email {Email}", loginModel.Email);

            // Always authenticate against the "Organisations" realm
            const string realm = "Organisations";
            var tokenEndpoint = $"{_config.BaseUrl}/realms/{realm}/protocol/openid-connect/token";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("client_id", _config.ClientId),
                new KeyValuePair<string, string>("client_secret", _config.ClientSecret),
                new KeyValuePair<string, string>("username", loginModel.Email),
                new KeyValuePair<string, string>("password", loginModel.Password),
                new KeyValuePair<string, string>("scope", "openid profile email")
            });

            var response = await _httpClient.PostAsync(tokenEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                // Don't log error details or username for security
                _logger.LogWarning("Authentication failed for email {Email}", loginModel.Email);

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

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogWarning("Authentication response missing access token for email {Email}", loginModel.Email);
                return new AuthenticationResponse
                {
                    Success = false,
                    ErrorMessage = "Authentication failed"
                };
            }

            // Get user info and fetch organization memberships
            var userInfo = await GetUserInfoAsync(accessToken);

            if (userInfo == null)
            {
                return new AuthenticationResponse
                {
                    Success = false,
                    ErrorMessage = "Unable to retrieve user information"
                };
            }

            var organizations = await GetOrganizationsForUserAsync(userInfo.UserId);

            if (organizations.Count == 0)
            {
                _logger.LogWarning("No organizations found for user {UserId}", userInfo.UserId);
                return new AuthenticationResponse
                {
                    Success = false,
                    ErrorMessage = "No organizations found for this account"
                };
            }

            var organizationInfos = organizations
                .Where(org => !string.IsNullOrWhiteSpace(org.Id))
                .Select(org => new OrganizationInfo
                {
                    Id = org.Id,
                    Name = org.Name ?? string.Empty
                })
                .ToList();

            if (organizationInfos.Count == 1)
            {
                var org = organizationInfos[0];
                userInfo.OrganizationId = org.Id;
                userInfo.Organization = org.Name;

                _logger.LogInformation("Successfully authenticated user for organization {Organization}", org.Name);

                return new AuthenticationResponse
                {
                    Success = true,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresIn = expiresIn,
                    UserInfo = userInfo,
                    Organizations = organizationInfos
                };
            }

            _logger.LogInformation("Authenticated user {UserId} with {Count} organizations", userInfo.UserId, organizationInfos.Count);

            return new AuthenticationResponse
            {
                Success = true,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = expiresIn,
                UserInfo = userInfo,
                RequiresOrganizationSelection = true,
                Organizations = organizationInfos
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

    public async Task<UserInfo?> GetUserInfoForOrganizationAsync(string accessToken, string organizationId)
    {
        var userInfo = await GetUserInfoAsync(accessToken);
        if (userInfo == null)
        {
            return null;
        }

        var organization = await GetOrganizationByIdAsync(organizationId);
        if (organization == null)
        {
            _logger.LogWarning("Organization {OrganizationId} not found", organizationId);
            return null;
        }

        var isMember = await VerifyOrganizationMembershipByIdAsync(userInfo.UserId, organizationId);
        if (!isMember)
        {
            _logger.LogWarning("User {UserId} is not a member of organization {OrganizationId}", userInfo.UserId, organizationId);
            return null;
        }

        userInfo.OrganizationId = organizationId;
        userInfo.Organization = organization.Name ?? string.Empty;

        return userInfo;
    }

    private async Task<UserInfo?> GetUserInfoAsync(string accessToken)
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

            return new UserInfo
            {
                UserId = userId,
                Username = userInfoJson["preferred_username"]?.ToString() ?? string.Empty,
                Email = userInfoJson["email"]?.ToString() ?? string.Empty,
                FirstName = userInfoJson["given_name"]?.ToString() ?? string.Empty,
                LastName = userInfoJson["family_name"]?.ToString() ?? string.Empty,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user info");
            return null;
        }
    }

    public async Task<List<OrganizationInfo>> GetOrganizationsForAccessTokenAsync(string accessToken)
    {
        var userInfo = await GetUserInfoAsync(accessToken);
        if (userInfo == null || string.IsNullOrWhiteSpace(userInfo.UserId))
        {
            return new List<OrganizationInfo>();
        }

        var organizations = await GetOrganizationsForUserAsync(userInfo.UserId);
        return organizations
            .Where(org => !string.IsNullOrWhiteSpace(org.Id))
            .Select(org => new OrganizationInfo
            {
                Id = org.Id,
                Name = org.Name ?? string.Empty
            })
            .ToList();
    }

    private async Task<List<OrganizationRepresentation>> GetOrganizationsForUserAsync(string userId)
    {
        try
        {
            const string realm = "Organisations";
            var endpoint = $"{_config.BaseUrl}/admin/realms/{realm}/organizations/members/{userId}/organizations";

            var adminRequest = await _requestHelper.CreateHttpRequestMessage(
                HttpMethod.Get,
                endpoint,
                null,
                new Helpers.ContentBuilders.JsonContentBuilder());

            var response = await _requestHelper.SendAsync(adminRequest);
            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<List<OrganizationRepresentation>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }) ?? new List<OrganizationRepresentation>();
        }
        catch (KeycloakException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return await GetOrganizationsForUserFallbackAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting organizations for user {UserId}", userId);
            return new List<OrganizationRepresentation>();
        }
    }

    private async Task<List<OrganizationRepresentation>> GetOrganizationsForUserFallbackAsync(string userId)
    {
        try
        {
            const string realm = "Organisations";
            var query = HttpQueryCreator.ToQueryString(new Dictionary<string, object?>
            {
                ["member"] = userId
            });
            var endpoint = $"{_config.BaseUrl}/admin/realms/{realm}/organizations{query}";

            var adminRequest = await _requestHelper.CreateHttpRequestMessage(
                HttpMethod.Get,
                endpoint,
                null,
                new Helpers.ContentBuilders.JsonContentBuilder());

            var response = await _requestHelper.SendAsync(adminRequest);
            var json = await response.Content.ReadAsStringAsync();

            var organizations = JsonSerializer.Deserialize<List<OrganizationRepresentation>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }) ?? new List<OrganizationRepresentation>();

            return await FilterOrganizationsByMembershipAsync(userId, organizations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting organizations for user {UserId} via fallback", userId);
            return new List<OrganizationRepresentation>();
        }
    }

    private async Task<List<OrganizationRepresentation>> FilterOrganizationsByMembershipAsync(
        string userId,
        List<OrganizationRepresentation> organizations)
    {
        if (organizations.Count == 0)
        {
            return organizations;
        }

        var membershipChecks = organizations.Select(async org =>
        {
            var isMember = !string.IsNullOrWhiteSpace(org.Id) &&
                await VerifyOrganizationMembershipByIdAsync(userId, org.Id);
            return (org, isMember);
        });

        var results = await Task.WhenAll(membershipChecks);
        return results.Where(result => result.isMember).Select(result => result.org).ToList();
    }

    private async Task<bool> VerifyOrganizationMembershipByIdAsync(string userId, string organizationId)
    {
        try
        {
            const string realm = "Organisations";
            var endpoint = $"{_config.BaseUrl}/admin/realms/{realm}/organizations/{organizationId}/members/{userId}";

            var adminRequest = await _requestHelper.CreateHttpRequestMessage(
                HttpMethod.Get,
                endpoint,
                null,
                new Helpers.ContentBuilders.JsonContentBuilder());

            await _requestHelper.SendAsync(adminRequest);
            return true;
        }
        catch (KeycloakException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("User {UserId} is not a member of organization {OrganizationId}", userId, organizationId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying organization membership for user {UserId}", userId);
            return false;
        }
    }

    private async Task<OrganizationRepresentation?> GetOrganizationByIdAsync(string organizationId)
    {
        try
        {
            const string realm = "Organisations";
            var endpoint = $"{_config.BaseUrl}/admin/realms/{realm}/organizations/{organizationId}";

            var adminRequest = await _requestHelper.CreateHttpRequestMessage(
                HttpMethod.Get,
                endpoint,
                null,
                new Helpers.ContentBuilders.JsonContentBuilder());

            var response = await _requestHelper.SendAsync(adminRequest);
            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<OrganizationRepresentation>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting organization {OrganizationId}", organizationId);
            return null;
        }
    }
}
