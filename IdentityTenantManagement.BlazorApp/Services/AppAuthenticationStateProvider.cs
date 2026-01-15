using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace IdentityTenantManagement.BlazorApp.Services;

public class AppAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly AuthenticationService _authService;

    public AppAuthenticationStateProvider(AuthenticationService authService)
    {
        _authService = authService;
        _authService.OnAuthenticationStateChanged +=
            () => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public override Task<Microsoft.AspNetCore.Components.Authorization.AuthenticationState> GetAuthenticationStateAsync()
    {
        return GetStateInternalAsync();

        async Task<Microsoft.AspNetCore.Components.Authorization.AuthenticationState> GetStateInternalAsync()
        {
            await _authService.EnsureStateLoadedAsync();

            if (!_authService.CurrentState.IsAuthenticated || _authService.CurrentState.UserInfo == null)
            {
                return new Microsoft.AspNetCore.Components.Authorization.AuthenticationState(
                    new ClaimsPrincipal(new ClaimsIdentity()));
            }

            var claims = new List<Claim>
            {
                new("sub", _authService.CurrentState.UserInfo.UserId),
                new("tenant_id", _authService.CurrentState.UserInfo.OrganizationId),
                new("preferred_username", _authService.CurrentState.UserInfo.Username)
            };

            var identity = new ClaimsIdentity(claims, "local");
            return new Microsoft.AspNetCore.Components.Authorization.AuthenticationState(
                new ClaimsPrincipal(identity));
        }
    }
}
