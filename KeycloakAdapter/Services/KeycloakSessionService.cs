using KeycloakAdapter.Helpers;
using KeycloakAdapter.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace KeycloakAdapter.Services;

public class KeycloakSessionService : KeycloakServiceBase, IKeycloakSessionService
{
    private readonly AsyncRetryPolicy _retryPolicy;

    public KeycloakSessionService(
        IOptions<KeycloakConfig> config,
        IKCRequestHelper requestHelper,
        ILogger<KeycloakSessionService> logger)
        : base(config, requestHelper, logger)
    {
        // Configure Polly retry policy (3 retries, exponential backoff)
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // 2s, 4s, 8s
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    Logger.LogWarning(
                        "Retry {RetryCount} after {Delay}s due to: {Exception}",
                        retryCount, timeSpan.TotalSeconds, exception.Message);
                });
    }

    public async Task<bool> RevokeAllUserSessionsAsync(string keycloakUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = BuildEndpoint($"users/{keycloakUserId}/logout");

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var request = await RequestHelper.CreateHttpRequestMessage(
                    HttpMethod.Post,
                    endpoint,
                    null,
                    new Helpers.ContentBuilders.JsonContentBuilder());

                return await RequestHelper.SendAsync(request);
            });

            if (response.IsSuccessStatusCode)
            {
                Logger.LogInformation("Successfully revoked all sessions for user {UserId}", keycloakUserId);
                return true;
            }
            else
            {
                Logger.LogWarning(
                    "Failed to revoke sessions for user {UserId}. Status: {StatusCode}",
                    keycloakUserId, response.StatusCode);
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Session revocation for user {UserId} was cancelled (timeout)", keycloakUserId);
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error revoking sessions for user {UserId}", keycloakUserId);
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetActiveSessionsAsync(string keycloakUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = BuildEndpoint($"users/{keycloakUserId}/sessions");

            var sessions = await _retryPolicy.ExecuteAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await GetAsync<List<Dictionary<string, object>>>(endpoint);
            });

            var sessionIds = sessions
                .Where(s => s.ContainsKey("id"))
                .Select(s => s["id"].ToString() ?? string.Empty)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            Logger.LogInformation("Found {Count} active sessions for user {UserId}", sessionIds.Count, keycloakUserId);
            return sessionIds;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting active sessions for user {UserId}", keycloakUserId);
            return Enumerable.Empty<string>();
        }
    }

    public async Task<bool> RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = BuildEndpoint($"sessions/{sessionId}");

            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await DeleteAsync(endpoint);
            });

            if (response.IsSuccessStatusCode)
            {
                Logger.LogInformation("Successfully revoked session {SessionId}", sessionId);
                return true;
            }
            else
            {
                Logger.LogWarning(
                    "Failed to revoke session {SessionId}. Status: {StatusCode}",
                    sessionId, response.StatusCode);
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Session revocation for session {SessionId} was cancelled (timeout)", sessionId);
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error revoking session {SessionId}", sessionId);
            return false;
        }
    }
}
