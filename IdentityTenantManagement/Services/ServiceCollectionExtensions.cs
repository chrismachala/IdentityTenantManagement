using IdentityTenantManagementDatabase.Repositories;

namespace IdentityTenantManagement.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register repositories (if needed for direct injection)
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();

        // Register application services
        services.AddScoped<IOnboardingService, OnboardingService>();
        // Add other core business services here

        return services;
    }
}