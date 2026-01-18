using KeycloakAdapter.Helpers;
using KeycloakAdapter.Models;
using KeycloakAdapter.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace KeycloakAdapter.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKeycloakIntegration(this IServiceCollection services, IConfiguration config)
    {
        var keycloakConfig = config.GetSection("KeycloakConfig").Get<KeycloakConfig>()
            ?? throw new InvalidOperationException("KeycloakConfig section is missing from configuration");

        services.Configure<KeycloakConfig>(config.GetSection("KeycloakConfig"));

        // Configure JWT Bearer authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            // Keycloak issuer URL: {BaseUrl}/realms/{Realm}
            options.Authority = $"{keycloakConfig.BaseUrl}/realms/{keycloakConfig.Realm}";
            options.Audience = keycloakConfig.ClientId;

            // For development with HTTP Keycloak
            options.RequireHttpsMetadata = keycloakConfig.BaseUrl.StartsWith("https://");

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"{keycloakConfig.BaseUrl}/realms/{keycloakConfig.Realm}",
                ValidateAudience = true,
                ValidAudiences = new[] { keycloakConfig.ClientId, "account" },
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });

        services.AddAuthorization();

        services.AddHttpClient<IKCRequestHelper, KCRequestHelper>();
        services.AddScoped<IKCRequestHelper, KCRequestHelper>();
        services.AddScoped<IKCOrganisationService, KCOrganisationService>();
        services.AddScoped<IKCUserService, KCUserService>();
        services.AddScoped<IKCAuthenticationService, KCAuthenticationService>();
        services.AddScoped<IKeycloakSessionService, KeycloakSessionService>();

        return services;
    }
}