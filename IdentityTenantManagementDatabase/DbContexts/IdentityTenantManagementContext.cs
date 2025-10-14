namespace IdentityTenantManagementDatabase.DbContexts;
using Microsoft.EntityFrameworkCore;
using Models;
 

public class IdentityTenantManagementContext : DbContext
{
    public IdentityTenantManagementContext(DbContextOptions<IdentityTenantManagementContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<IdentityProvider> IdentityProviders => Set<IdentityProvider>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<ExternalIdentityEntityType> ExternalIdentityEntityTypes => Set<ExternalIdentityEntityType>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<IdentityProvider>().HasData(new IdentityProvider
        {
            Id = Guid.Parse("049284C1-FF29-4F28-869F-F64300B69719"), // fixed deterministic ID
            Name = "Keycloak",
            ProviderType = "oidc",
            BaseUrl = "http://localhost:8080/",
            CreatedAt = new DateTime(2025, 10, 14, 1, 37, 0, DateTimeKind.Utc)
        });

        // Seed ExternalIdentityEntityTypes
        modelBuilder.Entity<ExternalIdentityEntityType>().HasData(
            new ExternalIdentityEntityType
            {
                Id = Guid.Parse("86E6890C-0B3F-4278-BD92-A4FD2EA55413"),
                EntityType = "user"
            },
            new ExternalIdentityEntityType
            {
                Id = Guid.Parse("F6D6E7FC-8998-4B77-AD31-552E5C76C3DD"),
                EntityType = "tenant"
            }
        );

        // Configure ExternalIdentity relationships
        modelBuilder.Entity<ExternalIdentity>()
            .HasOne(e => e.EntityType)
            .WithMany(et => et.ExternalIdentities)
            .HasForeignKey(e => e.EntityTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExternalIdentity>()
            .HasOne(e => e.Provider)
            .WithMany(p => p.ExternalIdentities)
            .HasForeignKey(e => e.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TenantUser>().HasKey(tu => tu.Id);

        modelBuilder.Entity<TenantUser>()
            .HasIndex(tu => new { tu.TenantId, tu.UserId })
            .IsUnique();

        modelBuilder.Entity<TenantDomain>()
            .HasIndex(d => d.Domain)
            .IsUnique();
        
        modelBuilder.Entity<TenantDomain>()
            .HasOne(d => d.Tenant)
            .WithMany(o => o.Domains)
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TenantUser>()
            .HasOne(ou => ou.Tenant)
            .WithMany()
            .HasForeignKey(ou => ou.TenantId)
            .OnDelete(DeleteBehavior.NoAction); 

        modelBuilder.Entity<TenantUser>()
            .HasOne(ou => ou.User)
            .WithMany()
            .HasForeignKey(ou => ou.UserId)
            .OnDelete(DeleteBehavior.NoAction);  
    }

}