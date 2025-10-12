using IdentityTenantManagement.EFCore;
using IdentityTenantManagement.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagement.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly IdentityTenantManagementContext _context;

    public TenantRepository(IdentityTenantManagementContext context)
    {
        _context = context;
    }

    public async Task<Tenant?> GetByIdAsync(Guid id)
    {
        return await _context.Tenants.FindAsync(id);
    }

    public async Task<IEnumerable<Tenant>> GetAllAsync()
    {
        return await _context.Tenants.ToListAsync();
    }

    public async Task<Tenant?> GetByDomainAsync(string domain)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.SDomain == domain);
    }

    public async Task<Tenant?> GetByNameAsync(string name)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.SName == name);
    }

    public async Task AddAsync(Tenant entity)
    {
        await _context.Tenants.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Tenant entity)
    {
        _context.Tenants.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var tenant = await GetByIdAsync(id);
        if (tenant == null)
        {
            throw new NotFoundException(nameof(Tenant), id.ToString());
        }

        _context.Tenants.Remove(tenant);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Tenants.AnyAsync(t => t.GTenantId == id);
    }
}
