using IdentityTenantManagementDatabase.DbContexts;
using IdentityTenantManagementDatabase.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagementDatabase.Repositories;

public class ExternalIdentityRepository : IExternalIdentityRepository
{
    private readonly IdentityTenantManagementContext _context;

    public ExternalIdentityRepository(IdentityTenantManagementContext context)
    {
        _context = context;
    }

    public async Task<ExternalIdentity?> GetByIdAsync(Guid id)
    {
        return await _context.ExternalIdentities
            .Include(e => e.Provider)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<IEnumerable<ExternalIdentity>> GetAllAsync()
    {
        return await _context.ExternalIdentities
            .Include(e => e.Provider)
            .ToListAsync();
    }

    public async Task AddAsync(ExternalIdentity entity)
    {
        await _context.ExternalIdentities.AddAsync(entity);
    }

    public async Task UpdateAsync(ExternalIdentity entity)
    {
        _context.ExternalIdentities.Update(entity);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _context.ExternalIdentities.Remove(entity);
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.ExternalIdentities.AnyAsync(e => e.Id == id);
    }

    public async Task<ExternalIdentity?> GetByExternalIdentifierAsync(string externalIdentifier, Guid providerId)
    {
        return await _context.ExternalIdentities
            .Include(e => e.Provider)
            .FirstOrDefaultAsync(e => e.ExternalIdentifier == externalIdentifier && e.ProviderId == providerId);
    }

    public async Task<IEnumerable<ExternalIdentity>> GetByEntityAsync(Guid entityTypeId, Guid entityId)
    {
        return await _context.ExternalIdentities
            .Include(e => e.Provider)
            .Include(e => e.EntityType)
            .Where(e => e.EntityTypeId == entityTypeId && e.EntityId == entityId)
            .ToListAsync();
    }
}