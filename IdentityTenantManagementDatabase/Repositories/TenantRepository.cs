using IdentityTenantManagementDatabase.DbContexts;
using IdentityTenantManagementDatabase.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagementDatabase.Repositories;

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
            .Include(t => t.Domains)
            .FirstOrDefaultAsync(t => t.Domains.Any(d => d.Domain == domain));
    }

    public async Task<Tenant?> GetByNameAsync(string name)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.Name == name);
    }

    public async Task AddAsync(Tenant entity)
    {
        await _context.Tenants.AddAsync(entity);
    }

    public async Task UpdateAsync(Tenant entity)
    {
        _context.Tenants.Update(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var tenant = await GetByIdAsync(id);
        if (tenant == null)
        {
            throw new KeyNotFoundException($"Tenant with ID {id} was not found.");
        }

        _context.Tenants.Remove(tenant);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Tenants.AnyAsync(t => t.Id == id);
    }
}