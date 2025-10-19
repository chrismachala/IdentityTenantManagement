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
            .Include(tu => tu.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(tu => tu.Id == id);
    }

    public async Task<IEnumerable<TenantUser>> GetAllAsync()
    {
        return await _context.TenantUsers
            .Include(tu => tu.Tenant)
            .Include(tu => tu.User)
            .Include(tu => tu.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
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
            .Include(tu => tu.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .Where(tu => tu.TenantId == tenantId)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenantUser>> GetByUserIdAsync(Guid userId)
    {
        return await _context.TenantUsers
            .Include(tu => tu.Tenant)
            .Include(tu => tu.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .Where(tu => tu.UserId == userId)
            .ToListAsync();
    }

    public async Task<TenantUser?> GetByTenantAndUserIdAsync(Guid tenantId, Guid userId)
    {
        return await _context.TenantUsers
            .Include(tu => tu.Tenant)
            .Include(tu => tu.User)
            .Include(tu => tu.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId);
    }

    public async Task<List<string>> GetUserPermissionsAsync(Guid tenantId, Guid userId)
    {
        var tenantUser = await _context.TenantUsers
            .Include(tu => tu.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId);

        if (tenantUser == null)
            return new List<string>();

        // Get permissions from role
        var rolePermissions = tenantUser.Role.RolePermissions
            .Select(rp => rp.Permission.Name)
            .ToList();

        // Get user-specific permissions
        var userPermissions = await _context.UserPermissions
            .Where(up => up.TenantUserId == tenantUser.Id)
            .Include(up => up.Permission)
            .Select(up => up.Permission.Name)
            .ToListAsync();

        // Combine and return distinct permissions
        return rolePermissions.Union(userPermissions).Distinct().ToList();
    }
}