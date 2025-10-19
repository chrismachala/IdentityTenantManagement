using IdentityTenantManagementDatabase.DbContexts;
using IdentityTenantManagementDatabase.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenantManagementDatabase.Repositories;

public class PermissionRepository : IPermissionRepository
{
    private readonly IdentityTenantManagementContext _context;

    public PermissionRepository(IdentityTenantManagementContext context)
    {
        _context = context;
    }

    public async Task<Permission?> GetByIdAsync(Guid id)
    {
        return await _context.Permissions.FindAsync(id);
    }

    public async Task<IEnumerable<Permission>> GetAllAsync()
    {
        return await _context.Permissions.ToListAsync();
    }

    public async Task AddAsync(Permission entity)
    {
        await _context.Permissions.AddAsync(entity);
    }

    public async Task UpdateAsync(Permission entity)
    {
        _context.Permissions.Update(entity);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var permission = await GetByIdAsync(id);
        if (permission != null)
        {
            _context.Permissions.Remove(permission);
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Permissions.AnyAsync(p => p.Id == id);
    }

    public async Task<Permission?> GetByNameAsync(string name)
    {
        return await _context.Permissions
            .FirstOrDefaultAsync(p => p.Name == name);
    }

    public async Task<List<Permission>> GetByPermissionGroupIdAsync(Guid permissionGroupId)
    {
        return await _context.Permissions
            .Where(p => p.PermissionGroupId == permissionGroupId)
            .ToListAsync();
    }
}