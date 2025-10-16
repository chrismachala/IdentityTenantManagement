using IdentityTenantManagementDatabase.DbContexts;
using IdentityTenantManagementDatabase.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagementDatabase.Repositories;

public class IdentityProviderRepository : IIdentityProviderRepository
{
    private readonly IdentityTenantManagementContext _context;

    public IdentityProviderRepository(IdentityTenantManagementContext context)
    {
        _context = context;
    }

    public async Task<IdentityProvider?> GetByIdAsync(Guid id)
    {
        return await _context.IdentityProviders.FindAsync(id);
    }

    public async Task<IEnumerable<IdentityProvider>> GetAllAsync()
    {
        return await _context.IdentityProviders.ToListAsync();
    }

    public async Task AddAsync(IdentityProvider entity)
    {
        await _context.IdentityProviders.AddAsync(entity);
    }

    public async Task UpdateAsync(IdentityProvider entity)
    {
        _context.IdentityProviders.Update(entity);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _context.IdentityProviders.Remove(entity);
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.IdentityProviders.AnyAsync(p => p.Id == id);
    }

    public async Task<IdentityProvider?> GetByNameAsync(string name)
    {
        return await _context.IdentityProviders
            .FirstOrDefaultAsync(p => p.Name == name);
    }
}