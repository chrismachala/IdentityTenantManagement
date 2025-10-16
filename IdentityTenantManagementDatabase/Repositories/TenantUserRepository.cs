using IdentityTenantManagementDatabase.DbContexts;
using IdentityTenantManagementDatabase.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagementDatabase.Repositories;

public class TenantUserRepository : ITenantUserRepository
{
    private readonly IdentityTenantManagementContext _context;

    public TenantUserRepository(IdentityTenantManagementContext context)
    {
        _context = context;
    }

    public async Task<TenantUser?> GetByIdAsync(Guid id)
    {
        return await _context.TenantUsers
            .Include(tu => tu.Tenant)
            .Include(tu => tu.User)
            .FirstOrDefaultAsync(tu => tu.Id == id);
    }

    public async Task<IEnumerable<TenantUser>> GetAllAsync()
    {
        return await _context.TenantUsers
            .Include(tu => tu.Tenant)
            .Include(tu => tu.User)
            .ToListAsync();
    }

    public async Task AddAsync(TenantUser entity)
    {
        await _context.TenantUsers.AddAsync(entity);
    }

    public async Task UpdateAsync(TenantUser entity)
    {
        _context.TenantUsers.Update(entity);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _context.TenantUsers.Remove(entity);
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.TenantUsers.AnyAsync(tu => tu.Id == id);
    }

    public async Task<IEnumerable<TenantUser>> GetByTenantIdAsync(Guid tenantId)
    {
        return await _context.TenantUsers
            .Include(tu => tu.User)
            .Where(tu => tu.TenantId == tenantId)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenantUser>> GetByUserIdAsync(Guid userId)
    {
        return await _context.TenantUsers
            .Include(tu => tu.Tenant)
            .Where(tu => tu.UserId == userId)
            .ToListAsync();
    }
}