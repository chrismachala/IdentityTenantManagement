using System.Net.Http.Headers;
using System.Text;
using IdentityTenantManagement.Helpers.ContentBuilders;
using IdentityTenantManagement.Models.Keycloak; 
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq; 

namespace IdentityTenantManagement.Helpers;

public interface IKCRequestHelper
{
    Task<HttpRequestMessage> CreateHttpRequestMessage(HttpMethod method, string endpoint, object body, IHttpContentBuilder contentBuilder);
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request);

} 
public class KCRequestHelper : IKCRequestHelper
{
    private readonly KeycloakConfig _config;
    private readonly HttpClient _httpClient;

    public KCRequestHelper(IOptions<KeycloakConfig> config, IHttpClientFactory httpClientFactory)
    {
        _config = config.Value;
        _httpClient = httpClientFactory.CreateClient(); 
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
            throw new Exception($"Keycloak returned {response.StatusCode}: {content}");
        }
        
        return response;
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var tokenEndpoint = $"{_config.BaseUrl}/realms/{_config.Realm}/protocol/openid-connect/token";

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", _config.ClientId),
            new KeyValuePair<string, string>("client_secret", _config.ClientSecret)
        });

        var response = await _httpClient.PostAsync(tokenEndpoint, content);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        var tokenResponse = JObject.Parse(responseBody);

        return tokenResponse["access_token"]!.ToString();
    }
}
 