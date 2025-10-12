using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagement.EFCore;

public partial class IdentityTenantManagementContext : DbContext
{
    public IdentityTenantManagementContext()
    {
    }

    public IdentityTenantManagementContext(DbContextOptions<IdentityTenantManagementContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Tenant> Tenants { get; set; }

    public virtual DbSet<User> Users { get; set; }

    // protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //     => optionsBuilder.UseSqlServer("Name=ConnectionStrings.OnboardingDatabase");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.GTenantId);

            entity.Property(e => e.GTenantId)
                .ValueGeneratedNever()
                .HasColumnName("gTenantId");
            entity.Property(e => e.SDomain)
                .HasMaxLength(65)
                .HasColumnName("sDomain");
            entity.Property(e => e.SName)
                .HasMaxLength(500)
                .HasColumnName("sName");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.GUserId);

            entity.Property(e => e.GUserId)
                .ValueGeneratedNever()
                .HasColumnName("gUserId");
            entity.Property(e => e.SEmail)
                .HasMaxLength(255)
                .HasColumnName("sEmail");
            entity.Property(e => e.SFirstName)
                .HasMaxLength(50)
                .HasColumnName("sFirstName");
            entity.Property(e => e.SLastName)
                .HasMaxLength(50)
                .HasColumnName("sLastName");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
