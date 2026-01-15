using KeycloakAdapter.Helpers;
using KeycloakAdapter.Models;
using KeycloakAdapter.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KeycloakAdapter.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKeycloakIntegration(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<KeycloakConfig>(config.GetSection("KeycloakConfig"));
    
        services.AddHttpClient<IKCRequestHelper, KCRequestHelper>();
        services.AddScoped<IKCRequestHelper, KCRequestHelper>();
        services.AddScoped<IKCOrganisationService, KCOrganisationService>();
        services.AddScoped<IKCUserService, KCUserService>();
        services.AddScoped<IKCAuthenticationService, KCAuthenticationService>();
        services.AddScoped<IKCEventsService, KCEventsService>();
        services.AddScoped<IKeycloakSessionService, KeycloakSessionService>();
    
        return services;
    }
 
 
}