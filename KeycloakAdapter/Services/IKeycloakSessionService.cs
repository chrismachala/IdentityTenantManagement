namespace KeycloakAdapter.Services;

public interface IKeycloakSessionService
{
    Task<bool> RevokeAllUserSessionsAsync(string keycloakUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetActiveSessionsAsync(string keycloakUserId, CancellationToken cancellationToken = default);
    Task<bool> RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}
