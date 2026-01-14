using IdentityTenantManagementDatabase.DbContexts;
using IdentityTenantManagementDatabase.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagementDatabase.Repositories;

public class RolePermissionRepository : IRolePermissionRepository
{
    private readonly IdentityTenantManagementContext _context;

    public RolePermissionRepository(IdentityTenantManagementContext context)
    {
        _context = context;
    }

    public async Task<RolePermission?> GetByIdAsync(Guid id)
    {
        return await _context.Set<RolePermission>()
            .Include(rp => rp.Role)
            .Include(rp => rp.Permission)
            .FirstOrDefaultAsync(rp => rp.Id == id);
    }

    public async Task<IEnumerable<RolePermission>> GetAllAsync()
    {
        return await _context.Set<RolePermission>()
            .Include(rp => rp.Role)
            .Include(rp => rp.Permission)
            .ToListAsync();
    }

    public async Task AddAsync(RolePermission entity)
    {
        await _context.Set<RolePermission>().AddAsync(entity);
    }

    public async Task UpdateAsync(RolePermission entity)
    {
        _context.Set<RolePermission>().Update(entity);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _context.Set<RolePermission>().Remove(entity);
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Set<RolePermission>()
            .AnyAsync(rp => rp.Id == id);
    }

    public async Task<IEnumerable<RolePermission>> GetByRoleIdAsync(Guid roleId)
    {
        return await _context.Set<RolePermission>()
            .Include(rp => rp.Permission)
                .ThenInclude(p => p.PermissionGroup)
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync();
    }

    public async Task<RolePermission?> GetByRoleIdAndPermissionIdAsync(Guid roleId, Guid permissionId)
    {
        return await _context.Set<RolePermission>()
            .Include(rp => rp.Permission)
            .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);
    }

    public async Task<bool> HasPermissionAsync(Guid roleId, Guid permissionId)
    {
        return await _context.Set<RolePermission>()
            .AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);
    }

    public async Task<IEnumerable<RolePermission>> GetByPermissionIdAsync(Guid permissionId)
    {
        return await _context.Set<RolePermission>()
            .Include(rp => rp.Role)
            .Where(rp => rp.PermissionId == permissionId)
            .ToListAsync();
    }
}