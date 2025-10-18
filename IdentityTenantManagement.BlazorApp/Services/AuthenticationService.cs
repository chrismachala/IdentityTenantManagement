using System.Net.Http.Json;
using System.Timers;
using Timer = System.Timers.Timer;

namespace IdentityTenantManagement.BlazorApp.Services;

public class AuthenticationService : IDisposable
{
    private readonly HttpClient _httpClient;
    private AuthenticationState _currentState = new();
    private Timer? _expirationTimer;

    public event Action? OnAuthenticationStateChanged;

    public AuthenticationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public AuthenticationState CurrentState => _currentState;

    public async Task<LoginResult> LoginAsync(LoginModel loginModel)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Authentication/login", loginModel);

            if (!response.IsSuccessStatusCode)
            {
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = "Invalid credentials or organization"
                };
            }

            var authResponse = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();

            if (authResponse?.Success == true && authResponse.UserInfo != null)
            {
                _currentState = new AuthenticationState
                {
                    IsAuthenticated = true,
                    AccessToken = authResponse.AccessToken,
                    UserInfo = authResponse.UserInfo,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(authResponse.ExpiresIn)
                };

                // Set up auto-logout timer
                SetupExpirationTimer(authResponse.ExpiresIn);

                OnAuthenticationStateChanged?.Invoke();

                return new LoginResult { Success = true };
            }

            return new LoginResult
            {
                Success = false,
                ErrorMessage = authResponse?.ErrorMessage ?? "Login failed"
            };
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

    public void Logout()
    {
        _expirationTimer?.Stop();
        _expirationTimer?.Dispose();
        _expirationTimer = null;

        _currentState = new AuthenticationState();
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
    public string Organization { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AuthenticationResponse
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public string? ErrorMessage { get; set; }
    public UserInfo? UserInfo { get; set; }
}

public class UserInfo
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
}