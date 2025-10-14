using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IdentityTenantManagementDatabase.DbContexts;

public class IdentityTenantManagementContextFactory : IDesignTimeDbContextFactory<IdentityTenantManagementContext>
{
    public IdentityTenantManagementContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityTenantManagementContext>();
 
        optionsBuilder.UseSqlServer("Data Source=localhost\\SQLEXPRESS;Initial Catalog=IdentityTenantManagement;Trusted_Connection=True;Encrypt=False;");

        return new IdentityTenantManagementContext(optionsBuilder.Options);
    }
}