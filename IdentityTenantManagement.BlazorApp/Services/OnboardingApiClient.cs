using System.Net.Http.Json;
using System.Text.Json;
using KeycloakAdapter.Models;

namespace IdentityTenantManagement.BlazorApp.Services;

public class OnboardingApiClient
{
    private readonly HttpClient _httpClient;

    public OnboardingApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> OnboardOrganisationAsync(TenantUserOnboardingModel model)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1.0/Onboarding/OnboardOrganisation", model);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error onboarding organisation: {ex.Message}");
            return false;
        }
    }
}

public class TenantUserOnboardingModel
{
    public CreateTenantModel CreateTenantModel { get; set; } = new();
    public CreateUserModel CreateUserModel { get; set; } = new();
}