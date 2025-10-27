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
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<PermissionGroup> PermissionGroups => Set<PermissionGroup>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<TenantUserRole> TenantUserRoles => Set<TenantUserRole>();
    public DbSet<UserStatusType> UserStatusTypes => Set<UserStatusType>();
    public DbSet<GlobalSettings> GlobalSettings => Set<GlobalSettings>();
    public DbSet<RegistrationFailureLog> RegistrationFailureLogs => Set<RegistrationFailureLog>();

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

        // Configure TenantUserRole relationships (many-to-many between TenantUser and Role)
        modelBuilder.Entity<TenantUserRole>()
            .HasOne(tur => tur.TenantUser)
            .WithMany(tu => tu.TenantUserRoles)
            .HasForeignKey(tur => tur.TenantUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TenantUserRole>()
            .HasOne(tur => tur.Role)
            .WithMany(r => r.TenantUserRoles)
            .HasForeignKey(tur => tur.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint for TenantUserRole (prevent duplicate role assignments)
        modelBuilder.Entity<TenantUserRole>()
            .HasIndex(tur => new { tur.TenantUserId, tur.RoleId })
            .IsUnique();

        // Configure Permission-PermissionGroup relationship
        modelBuilder.Entity<Permission>()
            .HasOne(p => p.PermissionGroup)
            .WithMany(pg => pg.Permissions)
            .HasForeignKey(p => p.PermissionGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure RolePermission relationships
        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint for RolePermission
        modelBuilder.Entity<RolePermission>()
            .HasIndex(rp => new { rp.RoleId, rp.PermissionId })
            .IsUnique();

        // Configure UserPermission relationships
        modelBuilder.Entity<UserPermission>()
            .HasOne(up => up.TenantUser)
            .WithMany()
            .HasForeignKey(up => up.TenantUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserPermission>()
            .HasOne(up => up.Permission)
            .WithMany()
            .HasForeignKey(up => up.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserPermission>()
            .HasOne(up => up.GrantedByUser)
            .WithMany()
            .HasForeignKey(up => up.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint for UserPermission
        modelBuilder.Entity<UserPermission>()
            .HasIndex(up => new { up.TenantUserId, up.PermissionId })
            .IsUnique();

        // Configure User-UserStatusType relationship
        modelBuilder.Entity<User>()
            .HasOne(u => u.Status)
            .WithMany(ust => ust.Users)
            .HasForeignKey(u => u.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint for GlobalSettings.Key
        modelBuilder.Entity<GlobalSettings>()
            .HasIndex(gs => gs.Key)
            .IsUnique();

        // Seed UserStatusTypes
        var activeStatusId = Guid.Parse("90A89389-7891-4784-90DF-F556E95BCCD9");
        var inactiveStatusId = Guid.Parse("7F313E08-F83E-43DF-B550-C11014592AB7");
        var suspendedStatusId = Guid.Parse("52EE270F-D6DE-4F96-A578-3C8E84AF4B9B");
        var pendingStatusId = Guid.Parse("3981B8F5-FFBD-426A-ABDE-A723631B3536");

        modelBuilder.Entity<UserStatusType>().HasData(
            new UserStatusType
            {
                Id = activeStatusId,
                Name = "active",
                DisplayName = "Active",
                Description = "User account is active and can access the system",
                CreatedAt = new DateTime(2025, 10, 26, 0, 0, 0, DateTimeKind.Utc)
            },
            new UserStatusType
            {
                Id = inactiveStatusId,
                Name = "inactive",
                DisplayName = "Inactive",
                Description = "User account is inactive and cannot access the system",
                CreatedAt = new DateTime(2025, 10, 26, 0, 0, 0, DateTimeKind.Utc)
            },
            new UserStatusType
            {
                Id = suspendedStatusId,
                Name = "suspended",
                DisplayName = "Suspended",
                Description = "User account has been temporarily suspended",
                CreatedAt = new DateTime(2025, 10, 26, 0, 0, 0, DateTimeKind.Utc)
            },
            new UserStatusType
            {
                Id = pendingStatusId,
                Name = "pending",
                DisplayName = "Pending",
                Description = "User account is pending activation or verification",
                CreatedAt = new DateTime(2025, 10, 26, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // Seed PermissionGroups
        var systemAdminGroupId = Guid.Parse("1A2B3C4D-5E6F-7890-ABCD-EF1234567890");

        modelBuilder.Entity<PermissionGroup>().HasData(
            new PermissionGroup
            {
                Id = systemAdminGroupId,
                Name = "SystemAdministration",
                DisplayName = "System Administration",
                Description = "Permissions related to system administration and user management",
                CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // Seed Roles
        var orgAdminRoleId = Guid.Parse("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");
        var orgManagerRoleId = Guid.Parse("B2C3D4E5-F678-90AB-CDEF-123456789ABC");
        var orgUserRoleId = Guid.Parse("C3D4E5F6-7890-ABCD-EF12-3456789ABCDE");

        modelBuilder.Entity<Role>().HasData(
            new Role
            {
                Id = orgAdminRoleId,
                Name = "org-admin",
                DisplayName = "Organization Administrator",
                Description = "Full access to organization settings and user management",
                CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc)
            },
            new Role
            {
                Id = orgManagerRoleId,
                Name = "org-manager",
                DisplayName = "Organization Manager",
                Description = "Can invite and manage organization users",
                CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc)
            },
            new Role
            {
                Id = orgUserRoleId,
                Name = "org-user",
                DisplayName = "Organization User",
                Description = "Standard organization member with no special permissions",
                CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // Seed Permissions
        var inviteUsersId = Guid.Parse("D4E5F678-90AB-CDEF-1234-56789ABCDEF0");
        var viewUsersId = Guid.Parse("E5F67890-ABCD-EF12-3456-789ABCDEF012");
        var updateUsersId = Guid.Parse("F6789ABC-DEF1-2345-6789-ABCDEF012345");
        var deleteUsersId = Guid.Parse("0789ABCD-EF12-3456-789A-BCDEF0123456");
        var assignPermissionsId = Guid.Parse("189ABCDE-F123-4567-89AB-CDEF01234567");
        var updateOrgSettingsId = Guid.Parse("29ABCDEF-0123-4567-89AB-CDEF12345678");

        modelBuilder.Entity<Permission>().HasData(
            new Permission
            {
                Id = inviteUsersId,
                Name = "invite-users",
                DisplayName = "Invite Users",
                Description = "Invite new users to the organization",
                PermissionGroupId = systemAdminGroupId,
                CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc)
            },
            new Permission
            {
                Id = viewUsersId,
                Name = "view-users",
                DisplayName = "View Users",
                Description = "View organization users",
                PermissionGroupId = systemAdminGroupId,
                CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc)
            },
            new Permission
            {
                Id = updateUsersId,
                Name = "update-users",
                DisplayName = "Update Users",
                Description = "Update user information",
                PermissionGroupId = systemAdminGroupId,
                CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc)
            },
            new Permission
            {
                Id = deleteUsersId,
                Name = "delete-users",
                DisplayName = "Delete Users",
                Description = "Remove users from the organization",
                PermissionGroupId = systemAdminGroupId,
                CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc)
            },
            new Permission
            {
                Id = assignPermissionsId,
                Name = "assign-permissions",
                DisplayName = "Assign Permissions",
                Description = "Assign roles and permissions to users",
                PermissionGroupId = systemAdminGroupId,
                CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc)
            },
            new Permission
            {
                Id = updateOrgSettingsId,
                Name = "update-org-settings",
                DisplayName = "Update Organization Settings",
                Description = "Modify organization settings and configuration",
                PermissionGroupId = systemAdminGroupId,
                CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // Seed RolePermissions
        // org-admin: all permissions
        modelBuilder.Entity<RolePermission>().HasData(
            new RolePermission { Id = Guid.Parse("A0D5987E-B3AB-4927-912C-5C43EAB14199"), RoleId = orgAdminRoleId, PermissionId = inviteUsersId, CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc) },
            new RolePermission { Id = Guid.Parse("FC30F474-088F-4A84-8116-101669A019E8"), RoleId = orgAdminRoleId, PermissionId = viewUsersId, CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc) },
            new RolePermission { Id = Guid.Parse("4675A21C-87AF-4DB2-AEDD-C8BE3C193CBB"), RoleId = orgAdminRoleId, PermissionId = updateUsersId, CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc) },
            new RolePermission { Id = Guid.Parse("3A8966F3-2F08-4EAA-84DE-38B7E8CC4CE6"), RoleId = orgAdminRoleId, PermissionId = deleteUsersId, CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc) },
            new RolePermission { Id = Guid.Parse("C070512F-94B2-4870-95E2-CC0185E9D4DA"), RoleId = orgAdminRoleId, PermissionId = assignPermissionsId, CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc) },
            new RolePermission { Id = Guid.Parse("FFF5CBC7-0965-4BD1-9BE8-74314917625F"), RoleId = orgAdminRoleId, PermissionId = updateOrgSettingsId, CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc) },

            // org-manager: invite, view, update users
            new RolePermission { Id = Guid.Parse("AC52B94D-98C5-4ACA-A5CE-1769B4B1E8B3"), RoleId = orgManagerRoleId, PermissionId = inviteUsersId, CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc) },
            new RolePermission { Id = Guid.Parse("B7BA3AB6-1406-41DE-93FC-F085B164BEB8"), RoleId = orgManagerRoleId, PermissionId = viewUsersId, CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc) },
            new RolePermission { Id = Guid.Parse("59CE4598-6A76-443C-A5AF-21D95C22E98A"), RoleId = orgManagerRoleId, PermissionId = updateUsersId, CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc) }

            // org-user: no permissions
        );

        // Seed GlobalSettings
        modelBuilder.Entity<GlobalSettings>().HasData(
            new GlobalSettings
            {
                Id = Guid.Parse("CEB4C8AE-4AF4-4A94-AC54-56DA00FC7B1E"),
                Key = "RequirePermissionToGrant",
                Value = "true",
                Description = "When enabled, users must have a role or permission themselves before they can grant it to others",
                CreatedAt = new DateTime(2025, 10, 19, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }

}