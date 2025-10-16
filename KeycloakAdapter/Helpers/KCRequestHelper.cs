using System.Net.Http.Headers;
using KeycloakAdapter.Exceptions;
using KeycloakAdapter.Helpers.ContentBuilders;
using KeycloakAdapter.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace KeycloakAdapter.Helpers;

public interface IKCRequestHelper
{
    Task<HttpRequestMessage> CreateHttpRequestMessage(HttpMethod method, string endpoint, object body, IHttpContentBuilder contentBuilder);
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request);

}
public class KCRequestHelper : IKCRequestHelper
{
    private readonly KeycloakConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<KCRequestHelper> _logger;

    // Token caching
    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public KCRequestHelper(
        IOptions<KeycloakConfig> config,
        IHttpClientFactory httpClientFactory,
        ILogger<KCRequestHelper> logger)
    {
        _config = config.Value;
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
    }

    public async Task<HttpRequestMessage> CreateHttpRequestMessage(HttpMethod method, string endpoint, object body, IHttpContentBuilder contentBuilder)
    {
        var accessToken = await GetAccessTokenAsync();

        var request = new HttpRequestMessage(method, endpoint)
        {
            Content = contentBuilder.Build(body)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }



    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new KeycloakException(
                $"Keycloak API request failed: {response.ReasonPhrase}",
                response.StatusCode,
                content);
        }

        return response;
    }

    private async Task<string> GetAccessTokenAsync()
    {
        // Check if cached token is still valid (with 30 second buffer)
        if (_cachedToken != null && DateTime.UtcNow < _tokenExpiry.AddSeconds(-30))
        {
            _logger.LogDebug("Using cached Keycloak access token");
            return _cachedToken;
        }

        // Use semaphore to prevent multiple token requests
        await _tokenLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_cachedToken != null && DateTime.UtcNow < _tokenExpiry.AddSeconds(-30))
            {
                return _cachedToken;
            }

            _logger.LogDebug("Requesting new Keycloak access token");

            var tokenEndpoint = $"{_config.BaseUrl}/realms/{_config.Realm}/protocol/openid-connect/token";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _config.ClientId),
                new KeyValuePair<string, string>("client_secret", _config.ClientSecret)
            });

            var response = await _httpClient.PostAsync(tokenEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to obtain Keycloak access token: {Error}", errorContent);
                throw new KeycloakException(
                    "Failed to obtain access token from Keycloak",
                    response.StatusCode,
                    errorContent);
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var tokenResponse = JObject.Parse(responseBody);

            _cachedToken = tokenResponse["access_token"]!.ToString();

            // Get expiry time (default to 5 minutes if not specified)
            var expiresIn = tokenResponse["expires_in"]?.Value<int>() ?? 300;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);

            _logger.LogInformation("Successfully obtained Keycloak access token, expires in {Seconds} seconds", expiresIn);

            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }
}