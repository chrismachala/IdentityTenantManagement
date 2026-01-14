using IdentityTenantManagementDatabase.DbContexts;
using IdentityTenantManagementDatabase.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagementDatabase.Repositories;

public class UserPermissionRepository : IUserPermissionRepository
{
    private readonly IdentityTenantManagementContext _context;

    public UserPermissionRepository(IdentityTenantManagementContext context)
    {
        _context = context;
    }

    public async Task<UserPermission?> GetByIdAsync(Guid id)
    {
        return await _context.UserPermissions.FindAsync(id);
    }

    public async Task<IEnumerable<UserPermission>> GetAllAsync()
    {
        return await _context.UserPermissions.ToListAsync();
    }

    public async Task AddAsync(UserPermission entity)
    {
        await _context.UserPermissions.AddAsync(entity);
    }

    public async Task UpdateAsync(UserPermission entity)
    {
        _context.UserPermissions.Update(entity);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var userPermission = await GetByIdAsync(id);
        if (userPermission != null)
        {
            _context.UserPermissions.Remove(userPermission);
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.UserPermissions.AnyAsync(up => up.Id == id);
    }

    public async Task<List<UserPermission>> GetByTenantUserIdAsync(Guid tenantUserId)
    {
        return await _context.UserPermissions
            .Include(up => up.Permission)
                .ThenInclude(p => p.PermissionGroup)
            .Where(up => up.TenantUserId == tenantUserId)
            .ToListAsync();
    }

    public async Task<UserPermission?> GetByTenantUserIdAndPermissionIdAsync(Guid tenantUserId, Guid permissionId)
    {
        return await _context.UserPermissions
            .FirstOrDefaultAsync(up => up.TenantUserId == tenantUserId && up.PermissionId == permissionId);
    }

    public async Task<bool> HasPermissionAsync(Guid tenantUserId, Guid permissionId)
    {
        return await _context.UserPermissions
            .AnyAsync(up => up.TenantUserId == tenantUserId && up.PermissionId == permissionId);
    }
}
