using IdentityTenantManagement.Helpers;
using IdentityTenantManagement.Models.Keycloak;
using IdentityTenantManagement.Services.KeycloakServices;

namespace IdentityTenantManagement.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKeycloakIntegration(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<KeycloakConfig>(config.GetSection("Keycloak"));

        services.AddHttpClient<IKCRequestHelper, KCRequestHelper>();
        services.AddScoped<IKCRequestHelper, KCRequestHelper>();
        services.AddScoped<IKCOrganisationService,KCOrganisationService>();
        services.AddScoped<IKCUserService,KCUserService>();
        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IOnboardingService, OnboardingService>();
        // Add other core business services here

        return services;
    }
}