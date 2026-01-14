namespace IdentityTenantManagement.BlazorApp.Services;

public class AuthenticatedHttpClientHandler : DelegatingHandler
{
    private readonly AuthenticationService _authService;

    public AuthenticatedHttpClientHandler(AuthenticationService authService)
    {
        _authService = authService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Add the Authorization header if we have a valid access token
        if (_authService.CurrentState.IsAuthenticated && !string.IsNullOrEmpty(_authService.CurrentState.AccessToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                _authService.CurrentState.AccessToken
            );
        }

        // Add user context headers for the API (used for audit logging and permission checks)
        if (_authService.CurrentState.UserInfo != null)
        {
            var userId = _authService.CurrentState.UserInfo.UserId;
            var tenantId = _authService.CurrentState.UserInfo.OrganizationId;

            if (!string.IsNullOrEmpty(userId))
            {
                request.Headers.TryAddWithoutValidation("X-User-Id", userId);
                Console.WriteLine($"[AuthHandler] Adding X-User-Id header: {userId}");
            }
            else
            {
                Console.WriteLine("[AuthHandler] WARNING: UserId is empty, not adding X-User-Id header");
            }

            if (!string.IsNullOrEmpty(tenantId))
            {
                request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);
                Console.WriteLine($"[AuthHandler] Adding X-Tenant-Id header: {tenantId}");
            }
            else
            {
                Console.WriteLine("[AuthHandler] WARNING: OrganizationId is empty, not adding X-Tenant-Id header");
            }
        }
        else
        {
            Console.WriteLine("[AuthHandler] WARNING: UserInfo is null, not adding auth headers");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}