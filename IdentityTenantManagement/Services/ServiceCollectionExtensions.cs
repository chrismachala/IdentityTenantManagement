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
        services.AddScoped<ITenantUserRepository, TenantUserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();

        // Register application services
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<PermissionService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}