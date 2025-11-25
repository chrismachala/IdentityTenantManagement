using IdentityTenantManagementDatabase.DbContexts;
using IdentityTenantManagementDatabase.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagementDatabase.Repositories;

public class TenantUserProfileRepository : ITenantUserProfileRepository
{
    private readonly IdentityTenantManagementContext _context;

    public TenantUserProfileRepository(IdentityTenantManagementContext context)
    {
        _context = context;
    }

    public async Task<TenantUserProfile?> GetByIdAsync(Guid id)
    {
        return await _context.TenantUserProfiles
            .Include(tup => tup.UserProfile)
            .FirstOrDefaultAsync(tup => tup.Id == id);
    }

    public async Task<TenantUserProfile?> GetByTenantUserIdAsync(Guid tenantUserId)
    {
        return await _context.TenantUserProfiles
            .Include(tup => tup.UserProfile)
            .FirstOrDefaultAsync(tup => tup.TenantUserId == tenantUserId);
    }

    public async Task<IEnumerable<TenantUserProfile>> GetAllAsync()
    {
        return await _context.TenantUserProfiles
            .Include(tup => tup.UserProfile)
            .ToListAsync();
    }

    public async Task AddAsync(TenantUserProfile entity)
    {
        await _context.TenantUserProfiles.AddAsync(entity);
    }

    public async Task UpdateAsync(TenantUserProfile entity)
    {
        _context.TenantUserProfiles.Update(entity);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _context.TenantUserProfiles.Remove(entity);
        }
    }

    public Task<bool> ExistsAsync(Guid id)
    {
        return _context.TenantUserProfiles.AnyAsync(tup => tup.Id == id);
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }
}