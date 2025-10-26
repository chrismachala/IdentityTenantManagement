using IdentityTenantManagementDatabase.Repositories;

namespace IdentityTenantManagement.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register Unit of Work (provides access to all repositories)
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register application services
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<PermissionService>();
        services.AddScoped<IUserService, UserService>();

        // Register orchestration services
        services.AddScoped<IUserOrchestrationService, UserOrchestrationService>();
        services.AddScoped<ITenantOrchestrationService, TenantOrchestrationService>();

        return services;
    }
}