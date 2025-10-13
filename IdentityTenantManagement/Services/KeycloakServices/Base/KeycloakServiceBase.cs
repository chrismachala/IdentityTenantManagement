using System.Text.Json;
using IdentityTenantManagement.Exceptions;
using IdentityTenantManagement.Helpers;
using IdentityTenantManagement.Helpers.ContentBuilders;
using IdentityTenantManagement.Models.Keycloak;
using Microsoft.Extensions.Options;

namespace IdentityTenantManagement.Services.KeycloakServices.Base;

public abstract class KeycloakServiceBase
{
    protected readonly KeycloakConfig Config;
    protected readonly IKCRequestHelper RequestHelper;
    protected readonly ILogger Logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected KeycloakServiceBase(
        IOptions<KeycloakConfig> config,
        IKCRequestHelper requestHelper,
        ILogger logger)
    {
        Config = config.Value;
        RequestHelper = requestHelper;
        Logger = logger;
    }

    /// <summary>
    /// Builds a full endpoint URL for the Keycloak API
    /// </summary>
    protected string BuildEndpoint(string path)
    {
        return $"{Config.BaseUrl}/admin/realms/{Config.Realm}/{path}";
    }

    /// <summary>
    /// Sends a GET request and deserializes the response
    /// </summary>
    protected async Task<T> GetAsync<T>(string endpoint, string? query = null)
    {
        var fullEndpoint = string.IsNullOrEmpty(query) ? endpoint : $"{endpoint}{query}";

        Logger.LogDebug("GET request to {Endpoint}", fullEndpoint);

        var request = await RequestHelper.CreateHttpRequestMessage(
            HttpMethod.Get,
            fullEndpoint,
            null,
            new JsonContentBuilder());

        var response = await RequestHelper.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        var result = JsonSerializer.Deserialize<T>(json, JsonOptions);

        if (result == null)
        {
            throw new KeycloakException(
                $"Failed to deserialize response from {fullEndpoint}",
                response.StatusCode,
                json);
        }

        return result;
    }

    /// <summary>
    /// Sends a POST request with JSON body
    /// </summary>
    protected async Task<HttpResponseMessage> PostJsonAsync<T>(string endpoint, T body)
    {
        Logger.LogDebug("POST request to {Endpoint}", endpoint);

        var request = await RequestHelper.CreateHttpRequestMessage(
            HttpMethod.Post,
            endpoint,
            body,
            new JsonContentBuilder());

        return await RequestHelper.SendAsync(request);
    }

    /// <summary>
    /// Sends a POST request with form-encoded body
    /// </summary>
    protected async Task<HttpResponseMessage> PostFormAsync(string endpoint, object body)
    {
        Logger.LogDebug("POST form request to {Endpoint}", endpoint);

        var request = await RequestHelper.CreateHttpRequestMessage(
            HttpMethod.Post,
            endpoint,
            body,
            new FormUrlEncodedContentBuilder());

        return await RequestHelper.SendAsync(request);
    }

    /// <summary>
    /// Sends a DELETE request
    /// </summary>
    protected async Task<HttpResponseMessage> DeleteAsync(string endpoint)
    {
        Logger.LogDebug("DELETE request to {Endpoint}", endpoint);

        var request = await RequestHelper.CreateHttpRequestMessage(
            HttpMethod.Delete,
            endpoint,
            null,
            new JsonContentBuilder());

        return await RequestHelper.SendAsync(request);
    }

    /// <summary>
    /// Sends a PUT request with JSON body
    /// </summary>
    protected async Task<HttpResponseMessage> PutJsonAsync<T>(string endpoint, T body)
    {
        Logger.LogDebug("PUT request to {Endpoint}", endpoint);

        var request = await RequestHelper.CreateHttpRequestMessage(
            HttpMethod.Put,
            endpoint,
            body,
            new JsonContentBuilder());

        return await RequestHelper.SendAsync(request);
    }

    /// <summary>
    /// Gets the first item from a list or throws NotFoundException
    /// </summary>
    protected T GetFirstOrThrow<T>(List<T> items, string entityType, string searchCriteria)
    {
        if (items == null || items.Count == 0)
        {
            Logger.LogWarning("{EntityType} not found with criteria: {Criteria}", entityType, searchCriteria);
            throw new NotFoundException(entityType, searchCriteria);
        }

        return items[0];
    }
}