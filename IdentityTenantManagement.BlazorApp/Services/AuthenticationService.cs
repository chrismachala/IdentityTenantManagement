using System.Net.Http.Json;
using System.Timers;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Timer = System.Timers.Timer;

namespace IdentityTenantManagement.BlazorApp.Services;

public class AuthenticationService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ProtectedSessionStorage _sessionStorage;
    private AuthenticationState _currentState = new();
    private Timer? _expirationTimer;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private bool _isLoaded;

    public event Action? OnAuthenticationStateChanged;

    public AuthenticationService(HttpClient httpClient, ProtectedSessionStorage sessionStorage)
    {
        _httpClient = httpClient;
        _sessionStorage = sessionStorage;
    }

    public AuthenticationState CurrentState => _currentState;

    public async Task EnsureStateLoadedAsync()
    {
        if (_isLoaded)
        {
            return;
        }

        await _loadLock.WaitAsync();
        try
        {
            if (_isLoaded)
            {
                return;
            }

            var stored = await _sessionStorage.GetAsync<StoredAuthState>("auth_state");
            if (stored.Success && stored.Value != null)
            {
                if (stored.Value.ExpiresAt != null && stored.Value.ExpiresAt <= DateTime.UtcNow)
                {
                    await _sessionStorage.DeleteAsync("auth_state");
                    _currentState = new AuthenticationState();
                }
                else
                {
                    _currentState = new AuthenticationState
                    {
                        IsAuthenticated = stored.Value.IsAuthenticated,
                        AccessToken = stored.Value.AccessToken,
                        UserInfo = stored.Value.UserInfo,
                        ExpiresAt = stored.Value.ExpiresAt
                    };

                    if (_currentState.ExpiresAt.HasValue)
                    {
                        var remainingSeconds = (int)Math.Max((_currentState.ExpiresAt.Value - DateTime.UtcNow).TotalSeconds, 0);
                        SetupExpirationTimer(remainingSeconds);
                    }
                }
            }

            _isLoaded = true;
        }
        finally
        {
            _loadLock.Release();
        }

        OnAuthenticationStateChanged?.Invoke();
    }

    public async Task<LoginResult> LoginAsync(LoginModel loginModel)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1.0/Authentication/login", loginModel);

            if (!response.IsSuccessStatusCode)
            {
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = "Invalid credentials"
                };
            }

            var authResponse = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();

            if (authResponse?.Success == true && authResponse.RequiresOrganizationSelection)
            {
                return new LoginResult
                {
                    Success = true,
                    RequiresOrganizationSelection = true,
                    Organizations = authResponse.Organizations,
                    PendingAccessToken = authResponse.AccessToken ?? string.Empty,
                    PendingExpiresIn = authResponse.ExpiresIn
                };
            }

            return await FinalizeLoginAsync(authResponse);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Login error: {ex.Message}");
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "An error occurred during login"
            };
        }
    }

    public async Task<LoginResult> SelectOrganizationAsync(string accessToken, int expiresIn, string organizationId)
    {
        try
        {
            var selectionModel = new OrganizationSelectionModel
            {
                AccessToken = accessToken,
                ExpiresIn = expiresIn,
                OrganizationId = organizationId
            };

            var response = await _httpClient.PostAsJsonAsync("api/v1.0/Authentication/select-organization", selectionModel);

            if (!response.IsSuccessStatusCode)
            {
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = "Unable to select organization"
                };
            }

            var authResponse = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();
            return await FinalizeLoginAsync(authResponse);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Organization selection error: {ex.Message}");
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "An error occurred while selecting the organization"
            };
        }
    }

    public async Task<List<OrganizationInfo>> GetOrganizationsAsync()
    {
        try
        {
            if (!_currentState.IsAuthenticated || string.IsNullOrEmpty(_currentState.AccessToken))
            {
                return new List<OrganizationInfo>();
            }

            var request = new HttpRequestMessage(HttpMethod.Get, "api/v1.0/Authentication/organizations");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                _currentState.AccessToken);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return new List<OrganizationInfo>();
            }

            return await response.Content.ReadFromJsonAsync<List<OrganizationInfo>>() ?? new List<OrganizationInfo>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Get organizations error: {ex.Message}");
            return new List<OrganizationInfo>();
        }
    }

    public async Task<LoginResult> SwitchOrganizationAsync(string organizationId)
    {
        if (!_currentState.IsAuthenticated || string.IsNullOrEmpty(_currentState.AccessToken))
        {
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "You are not authenticated."
            };
        }

        if (!_currentState.ExpiresAt.HasValue)
        {
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "Session information is missing."
            };
        }

        var remainingSeconds = (int)Math.Max((_currentState.ExpiresAt.Value - DateTime.UtcNow).TotalSeconds, 0);
        if (remainingSeconds <= 0)
        {
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "Your session has expired. Please sign in again."
            };
        }

        return await SelectOrganizationAsync(_currentState.AccessToken, remainingSeconds, organizationId);
    }

    public void Logout()
    {
        _expirationTimer?.Stop();
        _expirationTimer?.Dispose();
        _expirationTimer = null;

        _currentState = new AuthenticationState();
        _ = _sessionStorage.DeleteAsync("auth_state");
        OnAuthenticationStateChanged?.Invoke();
    }

    private void SetupExpirationTimer(int expiresInSeconds)
    {
        // Clean up existing timer
        _expirationTimer?.Stop();
        _expirationTimer?.Dispose();

        // Auto-logout 30 seconds before actual expiration to be safe
        var logoutDelay = TimeSpan.FromSeconds(Math.Max(expiresInSeconds - 30, 0));

        _expirationTimer = new Timer(logoutDelay.TotalMilliseconds);
        _expirationTimer.Elapsed += (sender, args) =>
        {
            Logout();
        };
        _expirationTimer.AutoReset = false;
        _expirationTimer.Start();
    }

    public void Dispose()
    {
        _expirationTimer?.Stop();
        _expirationTimer?.Dispose();
    }

    private async Task<LoginResult> FinalizeLoginAsync(AuthenticationResponse? authResponse)
    {
        if (authResponse?.Success == true && authResponse.UserInfo != null)
        {
            // Fetch user permissions separately from database
            var permissions = new List<string>();
            if (!string.IsNullOrEmpty(authResponse.UserInfo.UserId) && !string.IsNullOrEmpty(authResponse.UserInfo.OrganizationId))
            {
                try
                {
                    var permissionsResponse = await _httpClient.GetAsync(
                        $"api/v1.0/Authentication/permissions/{authResponse.UserInfo.UserId}/{authResponse.UserInfo.OrganizationId}"
                    );

                    if (permissionsResponse.IsSuccessStatusCode)
                    {
                        permissions = await permissionsResponse.Content.ReadFromJsonAsync<List<string>>() ?? new List<string>();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching permissions: {ex.Message}");
                    // Continue with empty permissions rather than failing login
                }
            }

            authResponse.UserInfo.Permissions = permissions;

            _currentState = new AuthenticationState
            {
                IsAuthenticated = true,
                AccessToken = authResponse.AccessToken,
                UserInfo = authResponse.UserInfo,
                ExpiresAt = DateTime.UtcNow.AddSeconds(authResponse.ExpiresIn)
            };

            // Set up auto-logout timer
            SetupExpirationTimer(authResponse.ExpiresIn);

            await _sessionStorage.SetAsync("auth_state", new StoredAuthState
            {
                IsAuthenticated = _currentState.IsAuthenticated,
                AccessToken = _currentState.AccessToken,
                UserInfo = _currentState.UserInfo,
                ExpiresAt = _currentState.ExpiresAt
            });

            OnAuthenticationStateChanged?.Invoke();

            return new LoginResult { Success = true };
        }

        return new LoginResult
        {
            Success = false,
            ErrorMessage = authResponse?.ErrorMessage ?? "Login failed"
        };
    }
}

public class AuthenticationState
{
    public bool IsAuthenticated { get; set; }
    public string? AccessToken { get; set; }
    public UserInfo? UserInfo { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class LoginModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool RequiresOrganizationSelection { get; set; }
    public List<OrganizationInfo> Organizations { get; set; } = new();
    public string PendingAccessToken { get; set; } = string.Empty;
    public int PendingExpiresIn { get; set; }
}

public class AuthenticationResponse
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public string? ErrorMessage { get; set; }
    public UserInfo? UserInfo { get; set; }
    public bool RequiresOrganizationSelection { get; set; }
    public List<OrganizationInfo> Organizations { get; set; } = new();
}

public class UserInfo
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new List<string>();
}

public class OrganizationInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class OrganizationSelectionModel
{
    public string AccessToken { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}

public class StoredAuthState
{
    public bool IsAuthenticated { get; set; }
    public string? AccessToken { get; set; }
    public UserInfo? UserInfo { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
