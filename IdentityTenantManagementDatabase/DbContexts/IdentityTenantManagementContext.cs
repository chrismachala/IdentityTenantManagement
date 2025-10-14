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
        
        modelBuilder.Entity<TenantUser>()
            .HasKey(ou => new { ou.TenantId, ou.UserId });
        
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
 
        // modelBuilder.Entity<User>()
        //     .HasOne(u => u.Tenant)
        //     .WithMany(o => o.Users)
        //     .HasForeignKey(u => u.TenantId)
        //     .OnDelete(DeleteBehavior.Restrict);
    }

}