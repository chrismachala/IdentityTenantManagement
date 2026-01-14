using IdentityTenantManagementDatabase.DbContexts;
using IdentityTenantManagementDatabase.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagementDatabase.Repositories;

public class TenantUserRoleRepository : ITenantUserRoleRepository
{
    private readonly IdentityTenantManagementContext _context;

    public TenantUserRoleRepository(IdentityTenantManagementContext context)
    {
        _context = context;
    }

    public async Task<TenantUserRole?> GetByIdAsync(Guid id)
    {
        return await _context.Set<TenantUserRole>()
            .Include(tur => tur.TenantUser)
                .ThenInclude(tu => tu.User)
            .Include(tur => tur.Role)
            .FirstOrDefaultAsync(tur => tur.Id == id);
    }

    public async Task<IEnumerable<TenantUserRole>> GetAllAsync()
    {
        return await _context.Set<TenantUserRole>()
            .Include(tur => tur.TenantUser)
                .ThenInclude(tu => tu.User)
            .Include(tur => tur.Role)
            .ToListAsync();
    }

    public async Task AddAsync(TenantUserRole entity)
    {
        await _context.Set<TenantUserRole>().AddAsync(entity);
    }

    public async Task UpdateAsync(TenantUserRole entity)
    {
        _context.Set<TenantUserRole>().Update(entity);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _context.Set<TenantUserRole>().Remove(entity);
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Set<TenantUserRole>()
            .AnyAsync(tur => tur.Id == id);
    }

    public async Task<IEnumerable<TenantUserRole>> GetByTenantUserIdAsync(Guid tenantUserId)
    {
        return await _context.Set<TenantUserRole>()
            .Include(tur => tur.Role)
            .Where(tur => tur.TenantUserId == tenantUserId)
            .ToListAsync();
    }

    public async Task<TenantUserRole?> GetByTenantUserIdAndRoleIdAsync(Guid tenantUserId, Guid roleId)
    {
        return await _context.Set<TenantUserRole>()
            .Include(tur => tur.Role)
            .FirstOrDefaultAsync(tur => tur.TenantUserId == tenantUserId && tur.RoleId == roleId);
    }

    public async Task<bool> HasRoleAsync(Guid tenantUserId, Guid roleId)
    {
        return await _context.Set<TenantUserRole>()
            .AnyAsync(tur => tur.TenantUserId == tenantUserId && tur.RoleId == roleId);
    }

    public async Task<IEnumerable<TenantUserRole>> GetByRoleIdAsync(Guid roleId)
    {
        return await _context.Set<TenantUserRole>()
            .Include(tur => tur.TenantUser)
                .ThenInclude(tu => tu.User)
            .Where(tur => tur.RoleId == roleId)
            .ToListAsync();
    }
}